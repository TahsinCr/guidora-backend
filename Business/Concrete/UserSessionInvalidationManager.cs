using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using Core.DataAccess;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class UserSessionInvalidationManager : IUserSessionInvalidationService
    {
        private readonly IUserAuthenticationStateDal _authenticationStateDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly IDistributedCache _distributedCache;
        private readonly IUnitOfWork _unitOfWork;

        public UserSessionInvalidationManager(IUserAuthenticationStateDal authenticationStateDal,
            IUserSessionDal userSessionDal, IDistributedCache distributedCache, IUnitOfWork unitOfWork)
        {
            _authenticationStateDal = authenticationStateDal;
            _userSessionDal = userSessionDal;
            _distributedCache = distributedCache;
            _unitOfWork = unitOfWork;
        }

        public Task<IResult> InvalidateUserSessionsAsync(int userId, CancellationToken cancellationToken = default)
        {
            return InvalidateUsersSessionsAsync([userId], cancellationToken);
        }

        public async Task<IResult> InvalidateUsersSessionsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
        {
            if (userIds is null || userIds.Count == 0 || userIds.Any(id => id <= 0))
                return new ErrorResult("At least one valid user identifier is required.");

            using var transaction = Transaction.Current is null
                ? new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions
                    {
                        IsolationLevel = IsolationLevel.ReadCommitted,
                        Timeout = TransactionManager.DefaultTimeout
                    }, TransactionScopeAsyncFlowOption.Enabled)
                : new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);

            var distinctUserIds = userIds.Distinct().OrderBy(id => id).ToArray();

            var now = DateTime.UtcNow;
            await _authenticationStateDal.IncrementAuthorizationVersionsAsync(distinctUserIds, now,
                cancellationToken);

            var activeSessions = await _userSessionDal.GetAllAsync(session =>
                distinctUserIds.Contains(session.UserId) && !session.RevokedAt.HasValue, cancellationToken);
            foreach (var session in activeSessions)
            {
                session.RevokedAt = now;
                _userSessionDal.Update(session);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // The local scope joins a caller's ambient transaction when present.
            // Evict only after the actual outermost commit.
            var cacheKeys = activeSessions.Select(session => $"auth-session:{session.Id}").ToArray();
            Transaction.Current!.TransactionCompleted += (_, eventArgs) =>
            {
                if (eventArgs.Transaction?.TransactionInformation.Status == TransactionStatus.Committed)
                {
                    RemoveSessionValidationCache(cacheKeys);
                }
            };

            transaction.Complete();
            return new SuccessResult();
        }

        private void RemoveSessionValidationCache(string[] cacheKeys)
        {
            foreach (var cacheKey in cacheKeys)
            {
                _distributedCache.Remove(cacheKey);
            }
        }
    }
}
