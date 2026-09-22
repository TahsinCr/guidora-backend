using System;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Microsoft.Extensions.Caching.Distributed;

namespace Business.Concrete
{
    public class TokenValidationManager : ITokenValidationService
    {
        private readonly IUserService _userService;
        private readonly IUserAuthenticationStateDal _authenticationStateDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly IDistributedCache _distributedCache;

        public TokenValidationManager(IUserService userService,
            IUserAuthenticationStateDal authenticationStateDal, IUserSessionDal userSessionDal,
            IDistributedCache distributedCache)
        {
            _userService = userService;
            _authenticationStateDal = authenticationStateDal;
            _userSessionDal = userSessionDal;
            _distributedCache = distributedCache;
        }

        public IResult Validate(int userId, string sessionId, int authorizationVersion)
        {
            if (!Guid.TryParse(sessionId, out var parsedSessionId))
            {
                return new ErrorResult("Access token session is invalid.");
            }

            var cacheKey = $"auth-session:{parsedSessionId}";
            var cachedAuthorizationVersion = _distributedCache.GetString(cacheKey);
            if (cachedAuthorizationVersion == authorizationVersion.ToString())
            {
                return new SuccessResult();
            }

            var userResult = _userService.GetById(userId);
            if (!userResult.Success || !userResult.Data.Status)
            {
                return new ErrorResult("User account is inactive.");
            }

            var state = _authenticationStateDal.Get(item => item.UserId == userId);
            if (state == null || state.AuthorizationVersion != authorizationVersion)
            {
                return new ErrorResult("Access token authorization is no longer valid.");
            }

            var session = _userSessionDal.Get(item => item.Id == parsedSessionId && item.UserId == userId);
            if (session == null || session.RevokedAt.HasValue || session.ExpiresAt <= DateTime.UtcNow)
            {
                return new ErrorResult("Access token session is no longer valid.");
            }

            _distributedCache.SetString(cacheKey, authorizationVersion.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });

            return new SuccessResult();
        }
    }
}
