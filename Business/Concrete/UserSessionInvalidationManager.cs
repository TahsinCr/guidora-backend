using System;
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
            if (userId <= 0)
            {
                return new ErrorResult("A valid user identifier is required.");
            }

            var now = DateTime.UtcNow;
            var authenticationState = _authenticationStateDal.Get(item => item.UserId == userId);
            if (authenticationState is null)
            {
                // Keep the entity Added. Calling Update here would turn the new row into
                // an UPDATE against its unset/generated primary key.
                authenticationState = new UserAuthenticationState
                {
                    UserId = userId,
                    AuthorizationVersion = 2,
                    UpdatedAt = now
                };
                _authenticationStateDal.Add(authenticationState);
            }
            else
            {
                checked
                {
                    authenticationState.AuthorizationVersion++;
                }
                authenticationState.UpdatedAt = now;
                _authenticationStateDal.Update(authenticationState);
            }

            var activeSessions = _userSessionDal.GetAll(session =>
                session.UserId == userId && !session.RevokedAt.HasValue);
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
