using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Core.DataAccess;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class UserSessionInvalidationManager : IUserSessionInvalidationService
    {
        private const long SessionFamilyLockBase = 7_500_000_000L;
        private readonly IUserAuthenticationStateDal _authenticationStateDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly IDistributedCache _distributedCache;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserSessionInvalidationManager> _logger;

        public UserSessionInvalidationManager(IUserAuthenticationStateDal authenticationStateDal,
            IUserSessionDal userSessionDal, IDistributedCache distributedCache, IUnitOfWork unitOfWork,
            ILogger<UserSessionInvalidationManager> logger)
        {
            _authenticationStateDal = authenticationStateDal;
            _userSessionDal = userSessionDal;
            _distributedCache = distributedCache;
            _unitOfWork = unitOfWork;
            _logger = logger;
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

            // Serialize refresh rotation and user-driven revocation with invalidation.
            // The ordered acquisition also avoids reversed lock ordering in bulk calls.
            foreach (var userId in distinctUserIds)
                await _unitOfWork.AcquireTransactionLockAsync(SessionFamilyLockBase + userId, cancellationToken);

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
            Exception? firstFailure = null;
            var failedCount = 0;
            foreach (var cacheKey in cacheKeys)
            {
                try
                {
                    _distributedCache.Remove(cacheKey);
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                    failedCount++;
                }
            }
            if (failedCount > 0)
                _logger.LogError(firstFailure,
                    "Failed to evict {FailedCount} of {SessionCount} authorization session cache entries after commit.",
                    failedCount, cacheKeys.Length);
        }
    }
}
