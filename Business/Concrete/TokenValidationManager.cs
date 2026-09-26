using System;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Microsoft.Extensions.Caching.Distributed;

namespace Business.Concrete
{
    public class TokenValidationManager : ITokenValidationService
    {
        private readonly IUserDal _userDal;
        private readonly IUserAuthenticationStateDal _authenticationStateDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly IDistributedCache _distributedCache;

        public TokenValidationManager(IUserDal userDal,
            IUserAuthenticationStateDal authenticationStateDal, IUserSessionDal userSessionDal,
            IDistributedCache distributedCache)
        {
            _userDal = userDal;
            _authenticationStateDal = authenticationStateDal;
            _userSessionDal = userSessionDal;
            _distributedCache = distributedCache;
        }

        public async Task<IResult> ValidateAsync(int userId, string sessionId, int authorizationVersion,
            CancellationToken cancellationToken = default)
        {
            if (!Guid.TryParse(sessionId, out var parsedSessionId))
            {
                return new ErrorResult("Access token session is invalid.");
            }

            var cacheKey = $"auth-session:{parsedSessionId}";
            var cachedAuthorizationVersion = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedAuthorizationVersion == authorizationVersion.ToString())
            {
                return new SuccessResult();
            }

            var user = await _userDal.GetAsync(item => item.Id == userId, cancellationToken);
            if (user is not { Status: true })
            {
                return new ErrorResult("User account is inactive.");
            }

            var state = await _authenticationStateDal.GetAsync(item => item.UserId == userId, cancellationToken);
            if (state == null || state.AuthorizationVersion != authorizationVersion)
            {
                return new ErrorResult("Access token authorization is no longer valid.");
            }

            var session = await _userSessionDal.GetAsync(item => item.Id == parsedSessionId && item.UserId == userId,
                cancellationToken);
            var now = DateTime.UtcNow;
            if (session == null || session.RevokedAt.HasValue || session.ExpiresAt <= now ||
                session.AbsoluteExpiresAt <= now)
            {
                return new ErrorResult("Access token session is no longer valid.");
            }

            var cacheExpiresAt = now.AddMinutes(1);
            if (session.ExpiresAt < cacheExpiresAt) cacheExpiresAt = session.ExpiresAt;
            if (session.AbsoluteExpiresAt < cacheExpiresAt) cacheExpiresAt = session.AbsoluteExpiresAt;
            await _distributedCache.SetStringAsync(cacheKey, authorizationVersion.ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpiration = new DateTimeOffset(cacheExpiresAt) },
                cancellationToken);

            return new SuccessResult();
        }
    }
}
