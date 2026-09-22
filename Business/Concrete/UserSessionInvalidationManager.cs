using System;
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
            var authenticationState = GetOrCreateAuthenticationState(userId);
            authenticationState.AuthorizationVersion++;
            authenticationState.UpdatedAt = DateTime.UtcNow;
            _authenticationStateDal.Update(authenticationState);

            var activeSessions = _userSessionDal.GetAll(session =>
                session.UserId == userId && !session.RevokedAt.HasValue);
            foreach (var session in activeSessions)
            {
                session.RevokedAt = DateTime.UtcNow;
                _userSessionDal.Update(session);
                _distributedCache.Remove($"auth-session:{session.Id}");
            }

            _unitOfWork.SaveChanges();

            return new SuccessResult();
        }

        private UserAuthenticationState GetOrCreateAuthenticationState(int userId)
        {
            var state = _authenticationStateDal.Get(item => item.UserId == userId);
            if (state != null)
            {
                return state;
            }

            state = new UserAuthenticationState
            {
                UserId = userId,
                AuthorizationVersion = 1,
                UpdatedAt = DateTime.UtcNow
            };
            _authenticationStateDal.Add(state);
            return state;
        }
    }
}
