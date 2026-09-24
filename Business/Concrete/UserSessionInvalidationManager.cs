using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Microsoft.Extensions.Caching.Distributed;
using Core.DataAccess;

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

        public IResult InvalidateUserSessions(int userId)
        {
            return InvalidateUsersSessions([userId]);
        }

        public IResult InvalidateUsersSessions(IReadOnlyCollection<int> userIds)
        {
            if (userIds is null || userIds.Count == 0 || userIds.Any(id => id <= 0))
                return new ErrorResult("At least one valid user identifier is required.");

            var distinctUserIds = userIds.Distinct().ToArray();

            var now = DateTime.UtcNow;
            var authenticationStates = _authenticationStateDal.GetAll(item => distinctUserIds.Contains(item.UserId));
            var statesByUserId = authenticationStates.ToDictionary(item => item.UserId);
            var newStates = new List<UserAuthenticationState>();
            foreach (var userId in distinctUserIds)
            {
                if (!statesByUserId.TryGetValue(userId, out var authenticationState))
                {
                    // Keep new rows Added; updating generated-key entities here would issue invalid UPDATEs.
                    newStates.Add(new UserAuthenticationState
                    {
                        UserId = userId,
                        AuthorizationVersion = 2,
                        UpdatedAt = now
                    });
                    continue;
                }

                checked
                {
                    authenticationState.AuthorizationVersion++;
                }
                authenticationState.UpdatedAt = now;
                _authenticationStateDal.Update(authenticationState);
            }
            foreach (var state in newStates) _authenticationStateDal.Add(state);

            var activeSessions = _userSessionDal.GetAll(session =>
                distinctUserIds.Contains(session.UserId) && !session.RevokedAt.HasValue);
            foreach (var session in activeSessions)
            {
                session.RevokedAt = now;
                _userSessionDal.Update(session);
            }

            _unitOfWork.SaveChanges();

            // A caller may own an ambient TransactionScope (for example Ban/Restore).
            // SaveChanges only flushes into that scope, so defer cache eviction until
            // the outer transaction really commits. Without an ambient transaction,
            // SaveChanges above is the commit boundary.
            var cacheKeys = activeSessions.Select(session => $"auth-session:{session.Id}").ToArray();
            var ambientTransaction = Transaction.Current;
            if (ambientTransaction is null)
            {
                RemoveSessionValidationCache(cacheKeys);
            }
            else
            {
                ambientTransaction.TransactionCompleted += (_, eventArgs) =>
                {
                    if (eventArgs.Transaction?.TransactionInformation.Status == TransactionStatus.Committed)
                    {
                        RemoveSessionValidationCache(cacheKeys);
                    }
                };
            }

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
