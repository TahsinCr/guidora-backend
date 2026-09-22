using Business.Abstract;
using Business.Constants;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.DTOs;
using System;
using System.Text;
using System.Threading.Tasks;
using Core.Business;
using Core.Utilities.Security.Hashing;
using DataAccess.Abstract;
using Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Core.Aspects.Transaction;

namespace Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private readonly IUserService _userService;
        private readonly IRoleDal _roleDal;
        private readonly IUserRoleDal _userRoleDal;
        private readonly ITokenHelper _tokenHelper;
        private readonly IUserAuthenticationStateDal _authenticationStateDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly TokenOptions _tokenOptions;
        private readonly IDistributedCache _distributedCache;
        private readonly IUserSessionInvalidationService _sessionInvalidationService;

        public AuthManager(IUserService userService, IRoleDal roleDal, IUserRoleDal userRoleDal,
            ITokenHelper tokenHelper, IUserAuthenticationStateDal authenticationStateDal, IUserSessionDal userSessionDal, IConfiguration configuration,
            IDistributedCache distributedCache, IUserSessionInvalidationService sessionInvalidationService)
        {
            _userService = userService;
            _roleDal = roleDal;
            _userRoleDal = userRoleDal;
            _tokenHelper = tokenHelper;
            _authenticationStateDal = authenticationStateDal;
            _userSessionDal = userSessionDal;
            _tokenOptions = configuration.GetSection("TokenOptions").Get<TokenOptions>()
                ?? throw new InvalidOperationException("TokenOptions configuration is missing.");
            _distributedCache = distributedCache;
            _sessionInvalidationService = sessionInvalidationService;
        }

        [TransactionScopeAspect]
        public IDataResult<User> Register(UserForRegisterDto userForRegisterDto, string password)
        {
            byte[] passwordHash, passwordSalt;
            HashingHelper.CreatePasswordHash(password, out passwordHash, out passwordSalt);
            var user = new User
            {
                Email = userForRegisterDto.Email,
                FirstName = userForRegisterDto.FirstName,
                LastName = userForRegisterDto.LastName,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Status = true
            };


            var addResult = _userService.Add(user);
            if (!addResult.Success) return new ErrorDataResult<User>(addResult.Message);

            var defaultRole = _roleDal.Get(role => role.Code == "user" && role.IsActive);
            if (defaultRole == null)
            {
                return new ErrorDataResult<User>("Default user role is not configured.");
            }

            _userRoleDal.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });
            _authenticationStateDal.Add(new UserAuthenticationState
            {
                UserId = user.Id,
                AuthorizationVersion = 1,
                UpdatedAt = DateTime.UtcNow
            });

            return new SuccessDataResult<User>(user, Messages.UserRegistered);
        }

        public IDataResult<User> Login(UserForLoginDto userForLoginDto)
        {
            var userToCheck = _userService.GetByMail(userForLoginDto.Email);
            if (userToCheck.Data == null) return userToCheck;

            if (!userToCheck.Data.Status)
            {
                return new ErrorDataResult<User>("User account is inactive.");
            }

            if (!HashingHelper.VerifyPasswordHash(userForLoginDto.Password, userToCheck.Data.PasswordHash, userToCheck.Data.PasswordSalt))
            {
                return new ErrorDataResult<User>(Messages.PasswordError);
            }

            return new SuccessDataResult<User>(userToCheck.Data, Messages.SuccessfulLogin);
        }

        public IResult UserExists(string email)
        {
            if (_userService.GetByMail(email).Data != null)
            {
                return new ErrorResult(Messages.UserAlreadyExists);
            }
            return new SuccessResult();
        }

        public IDataResult<AuthenticationResultDto> CreateSession(User user, bool rememberMe, string deviceName)
        {
            if (!user.Status)
            {
                return new ErrorDataResult<AuthenticationResultDto>("User account is inactive.");
            }

            var authenticationState = GetOrCreateAuthenticationState(user.Id);
            var claims = _userService.GetClaims(user).Data;
            var refreshToken = RefreshTokenHasher.CreateToken();
            var now = DateTime.UtcNow;
            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RefreshTokenHash = RefreshTokenHasher.Hash(refreshToken),
                CreatedAt = now,
                ExpiresAt = now.AddDays(rememberMe
                    ? _tokenOptions.RememberMeExpirationDays
                    : _tokenOptions.RefreshTokenExpirationDays),
                IsPersistent = rememberMe,
                DeviceName = deviceName
            };

            _userSessionDal.Add(session);
            var accessToken = _tokenHelper.CreateToken(user, claims, session.Id, authenticationState.AuthorizationVersion);
            return new SuccessDataResult<AuthenticationResultDto>(new AuthenticationResultDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                IsPersistentSession = session.IsPersistent,
                RefreshTokenExpiration = session.ExpiresAt
            }, Messages.AccessTokenCreated);
        }

        public IDataResult<AuthenticationResultDto> RefreshSession(string refreshToken, string deviceName)
        {
            var session = _userSessionDal.Get(item => item.RefreshTokenHash == RefreshTokenHasher.Hash(refreshToken));
            if (session == null)
            {
                return new ErrorDataResult<AuthenticationResultDto>("Refresh session is invalid.");
            }

            if (session.RevokedAt.HasValue || session.ExpiresAt <= DateTime.UtcNow)
            {
                _sessionInvalidationService.InvalidateUserSessions(session.UserId);
                return new ErrorDataResult<AuthenticationResultDto>("Refresh session is no longer valid.");
            }

            var userResult = _userService.GetById(session.UserId);
            if (!userResult.Success || !userResult.Data.Status)
            {
                _sessionInvalidationService.InvalidateUserSessions(session.UserId);
                return new ErrorDataResult<AuthenticationResultDto>("User account is inactive.");
            }

            session.RevokedAt = DateTime.UtcNow;
            RemoveSessionValidationCache(session.Id);
            var newResult = CreateSession(userResult.Data, session.IsPersistent, deviceName);
            if (!newResult.Success)
            {
                return newResult;
            }

            session.ReplacedBySessionId = newResult.Data.AccessToken.SessionId;
            _userSessionDal.Update(session);

            var replacementSession = _userSessionDal.Get(item => item.Id == newResult.Data.AccessToken.SessionId);
            if (replacementSession != null)
            {
                var absoluteExpiration = session.CreatedAt.AddDays(_tokenOptions.SessionAbsoluteExpirationDays);
                if (replacementSession.ExpiresAt > absoluteExpiration)
                {
                    replacementSession.ExpiresAt = absoluteExpiration;
                    _userSessionDal.Update(replacementSession);
                    newResult.Data.RefreshTokenExpiration = replacementSession.ExpiresAt;
                }
            }

            return newResult;
        }

        public IResult RevokeSession(string refreshToken)
        {
            var session = _userSessionDal.Get(item => item.RefreshTokenHash == RefreshTokenHasher.Hash(refreshToken));
            if (session == null || session.RevokedAt.HasValue)
            {
                return new SuccessResult();
            }

            session.RevokedAt = DateTime.UtcNow;
            _userSessionDal.Update(session);
            RemoveSessionValidationCache(session.Id);
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

        private void RemoveSessionValidationCache(Guid sessionId)
        {
            _distributedCache.Remove($"auth-session:{sessionId}");
        }
    }
}
