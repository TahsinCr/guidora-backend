using Business.Abstract;
using Business.Constants;
using Business.ValidationRules.FluentValidation;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.DTOs;
using System;
using System.Threading.Tasks;
using Core.Utilities.Security.Hashing;
using DataAccess.Abstract;
using Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Core.Aspects.Transaction;
using Core.Aspects.Autofac.Validation;
using Core.DataAccess;
using System.Threading;
using System.Transactions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private const string InvalidCredentialsMessage = "Invalid email or password.";
        private const long SessionFamilyLockBase = 7_500_000_000L;
        private static readonly byte[] MissingUserHash = new byte[32];
        private static readonly byte[] MissingUserSalt = new byte[16];
        private readonly IUserService _userService;
        private readonly IRoleDal _roleDal;
        private readonly IUserRoleDal _userRoleDal;
        private readonly ITokenHelper _tokenHelper;
        private readonly IUserAuthenticationStateDal _authenticationStateDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly TokenOptions _tokenOptions;
        private readonly IDistributedCache _distributedCache;
        private readonly IUserSessionInvalidationService _sessionInvalidationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AuthManager> _logger;

        public AuthManager(IUserService userService, IRoleDal roleDal, IUserRoleDal userRoleDal,
            ITokenHelper tokenHelper, IUserAuthenticationStateDal authenticationStateDal, IUserSessionDal userSessionDal, IConfiguration configuration,
            IDistributedCache distributedCache, IUserSessionInvalidationService sessionInvalidationService,
            IUnitOfWork unitOfWork, ILogger<AuthManager> logger)
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
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        [ValidationAspect(typeof(UserForRegisterValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<User>> RegisterAsync(UserForRegisterDto userForRegisterDto, string password,
            CancellationToken cancellationToken = default)
        {
            var existence = await UserExistsAsync(userForRegisterDto.Email, cancellationToken);
            if (!existence.Success)
            {
                return new ConflictDataResult<User>(existence.Message);
            }

            byte[] passwordHash, passwordSalt;
            HashingHelper.CreatePasswordHash(password, out passwordHash, out passwordSalt);
            var user = new User
            {
                Email = userForRegisterDto.Email,
                FirstName = userForRegisterDto.FirstName,
                LastName = userForRegisterDto.LastName,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Status = true,
                CreatedAt = DateTime.UtcNow
            };


            var addResult = await _userService.RegisterUserAsync(user, cancellationToken);
            if (!addResult.Success) return new ErrorDataResult<User>(addResult.Message);

            var defaultRole = await _roleDal.GetAsync(role => role.Code == "user" && role.IsActive, cancellationToken);
            if (defaultRole == null)
            {
                return new ErrorDataResult<User>("Default user role is not configured.");
            }

            await _userRoleDal.AddAsync(new UserRole { UserId = user.Id, RoleId = defaultRole.Id }, cancellationToken);
            await _authenticationStateDal.AddAsync(new UserAuthenticationState
            {
                UserId = user.Id,
                AuthorizationVersion = 1,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SuccessDataResult<User>(user, Messages.UserRegistered);
        }

        [ValidationAspect(typeof(UserForLoginValidator))]
        public async Task<IDataResult<User>> LoginAsync(UserForLoginDto userForLoginDto,
            CancellationToken cancellationToken = default)
        {
            var userToCheck = await _userService.GetByMailAsync(userForLoginDto.Email, cancellationToken);
            if (userToCheck.Data == null)
            {
                // Match the password work performed for an existing PBKDF2 account.
                _ = HashingHelper.VerifyPasswordHash(userForLoginDto.Password, MissingUserHash, MissingUserSalt);
                return new ErrorDataResult<User>(InvalidCredentialsMessage);
            }

            var passwordValid = HashingHelper.VerifyPasswordHash(userForLoginDto.Password,
                userToCheck.Data.PasswordHash, userToCheck.Data.PasswordSalt);
            if (!userToCheck.Data.Status || !passwordValid)
                return new ErrorDataResult<User>(InvalidCredentialsMessage);

            if (HashingHelper.NeedsRehash(userToCheck.Data.PasswordHash, userToCheck.Data.PasswordSalt))
            {
                byte[] upgradedHash;
                byte[] upgradedSalt;
                HashingHelper.CreatePasswordHash(userForLoginDto.Password,
                    out upgradedHash, out upgradedSalt);
                userToCheck.Data.PasswordHash = upgradedHash;
                userToCheck.Data.PasswordSalt = upgradedSalt;
                await _userService.UpdateUserAsync(userToCheck.Data, cancellationToken);
            }

            return new SuccessDataResult<User>(userToCheck.Data, Messages.SuccessfulLogin);
        }

        public async Task<IResult> UserExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            if ((await _userService.GetByMailAsync(email, cancellationToken)).Data != null)
            {
                return new ConflictResult(Messages.UserAlreadyExists);
            }
            return new SuccessResult();
        }

        [TransactionScopeAspect]
        public async Task<IDataResult<AuthenticationResultDto>> CreateSessionAsync(User user, bool rememberMe,
            string deviceName, CancellationToken cancellationToken = default)
        {
            if (!user.Status)
            {
                return new ErrorDataResult<AuthenticationResultDto>(InvalidCredentialsMessage);
            }

            await _unitOfWork.AcquireTransactionLockAsync(SessionFamilyLockBase + user.Id,
                cancellationToken);
            var authenticationState = await GetOrCreateAuthenticationStateAsync(user.Id, cancellationToken);
            var claims = (await _userService.GetClaimsAsync(user, cancellationToken)).Data;
            var refreshToken = RefreshTokenHasher.CreateToken();
            var now = DateTime.UtcNow;
            var absoluteExpiresAt = now.AddDays(_tokenOptions.SessionAbsoluteExpirationDays);
            var configuredExpiresAt = now.AddDays(rememberMe
                ? _tokenOptions.RememberMeExpirationDays
                : _tokenOptions.RefreshTokenExpirationDays);
            var sessionId = Guid.NewGuid();
            var session = new UserSession
            {
                Id = sessionId,
                SessionFamilyId = sessionId,
                UserId = user.Id,
                RefreshTokenHash = RefreshTokenHasher.Hash(refreshToken),
                CreatedAt = now,
                LastUsedAt = now,
                AbsoluteExpiresAt = absoluteExpiresAt,
                ExpiresAt = configuredExpiresAt < absoluteExpiresAt ? configuredExpiresAt : absoluteExpiresAt,
                IsPersistent = rememberMe,
                DeviceName = deviceName
            };

            await _userSessionDal.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var accessToken = _tokenHelper.CreateToken(user, claims, session.Id, authenticationState.AuthorizationVersion);
            return new SuccessDataResult<AuthenticationResultDto>(new AuthenticationResultDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                IsPersistentSession = session.IsPersistent,
                RefreshTokenExpiration = session.ExpiresAt
            }, Messages.AccessTokenCreated);
        }

        public async Task<IDataResult<AuthenticationResultDto>> RefreshSessionAsync(string refreshToken,
            string deviceName, CancellationToken cancellationToken = default)
        {
            var session = await _userSessionDal.GetAsync(item => item.RefreshTokenHash == RefreshTokenHasher.Hash(refreshToken), cancellationToken);
            if (session == null)
            {
                return new ErrorDataResult<AuthenticationResultDto>("Refresh session is invalid.");
            }

            var validationTime = DateTime.UtcNow;
            if (session.AbsoluteExpiresAt <= validationTime)
                return new ErrorDataResult<AuthenticationResultDto>("Refresh session is no longer valid.");

            if (session.RevokedAt.HasValue)
            {
                await HandleReplayAsync(session, cancellationToken);
                return new ErrorDataResult<AuthenticationResultDto>("Refresh session is no longer valid.");
            }

            if (session.ExpiresAt <= validationTime)
                return new ErrorDataResult<AuthenticationResultDto>("Refresh session is no longer valid.");

            var userResult = await _userService.GetByIdAsync(session.UserId, cancellationToken);
            if (!userResult.Success || !userResult.Data.Status)
            {
                await _sessionInvalidationService.InvalidateUserSessionsAsync(session.UserId, cancellationToken);
                return new ErrorDataResult<AuthenticationResultDto>("User account is inactive.");
            }

            var now = DateTime.UtcNow;
            var nextRefreshToken = RefreshTokenHasher.CreateToken();
            var absoluteExpiration = session.AbsoluteExpiresAt;
            var configuredExpiration = now.AddDays(session.IsPersistent
                ? _tokenOptions.RememberMeExpirationDays
                : _tokenOptions.RefreshTokenExpirationDays);
            var replacement = new UserSession
            {
                Id = Guid.NewGuid(),
                SessionFamilyId = session.SessionFamilyId,
                UserId = session.UserId,
                RefreshTokenHash = RefreshTokenHasher.Hash(nextRefreshToken),
                CreatedAt = now,
                LastUsedAt = now,
                AbsoluteExpiresAt = session.AbsoluteExpiresAt,
                ExpiresAt = configuredExpiration < absoluteExpiration ? configuredExpiration : absoluteExpiration,
                IsPersistent = session.IsPersistent,
                DeviceName = deviceName
            };
            var rotationRejected = false;
            IDataResult<AuthenticationResultDto>? result = null;

            using (var transaction = new TransactionScope(TransactionScopeOption.Required,
                       new TransactionOptions
                       {
                           IsolationLevel = IsolationLevel.ReadCommitted,
                           Timeout = TransactionManager.DefaultTimeout
                       }, TransactionScopeAsyncFlowOption.Enabled))
            {
                await _unitOfWork.AcquireTransactionLockAsync(SessionFamilyLockBase + session.UserId,
                    cancellationToken);
                if (await _userSessionDal.TryRotateAsync(session.Id, replacement.Id, now, cancellationToken))
                {
                    var authenticationState = await GetOrCreateAuthenticationStateAsync(session.UserId, cancellationToken);
                    var claims = (await _userService.GetClaimsAsync(userResult.Data, cancellationToken)).Data;
                    await _userSessionDal.AddAsync(replacement, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    var accessToken = _tokenHelper.CreateToken(userResult.Data, claims, replacement.Id,
                        authenticationState.AuthorizationVersion);
                    result = new SuccessDataResult<AuthenticationResultDto>(new AuthenticationResultDto
                    {
                        AccessToken = accessToken,
                        RefreshToken = nextRefreshToken,
                        IsPersistentSession = replacement.IsPersistent,
                        RefreshTokenExpiration = replacement.ExpiresAt
                    }, Messages.AccessTokenCreated);
                    transaction.Complete();
                }
                else
                {
                    rotationRejected = true;
                    result = new ErrorDataResult<AuthenticationResultDto>(
                        "Refresh session has already been rotated. Authenticate again.");
                }
            }

            if (rotationRejected)
            {
                await HandleReplayAsync(session, cancellationToken);
                return result!;
            }

            await EvictSessionCacheAsync([session.Id]);
            return result!;
        }

        private async Task HandleReplayAsync(UserSession session, CancellationToken cancellationToken)
        {
            if (session.AbsoluteExpiresAt <= DateTime.UtcNow)
                return;

            using var transaction = new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TransactionManager.DefaultTimeout
                }, TransactionScopeAsyncFlowOption.Enabled);
            if (!await _userSessionDal.TryMarkReplayDetectedAsync(session.Id, DateTime.UtcNow, cancellationToken))
                return;

            var invalidation = await _sessionInvalidationService.InvalidateUserSessionsAsync(
                session.UserId, cancellationToken);
            if (!invalidation.Success)
                throw new InvalidOperationException("Refresh replay session invalidation failed.");
            transaction.Complete();
        }

        public async Task<IResult> RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var session = await _userSessionDal.GetAsync(item => item.RefreshTokenHash == RefreshTokenHasher.Hash(refreshToken), cancellationToken);
            if (session == null || session.RevokedAt.HasValue)
            {
                return new SuccessResult();
            }

            return await RevokeOwnedSessionAsync(session.UserId, session.Id, cancellationToken);
        }

        public async Task<IDataResult<PagedResult<UserSessionSummaryDto>>> GetSessionsAsync(int userId,
            Guid currentSessionId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || currentSessionId == Guid.Empty || page < 1 || pageSize is < 1 or > 100 ||
                (long)(page - 1) * pageSize > int.MaxValue)
                return new ErrorDataResult<PagedResult<UserSessionSummaryDto>>("Invalid session page.");

            var result = await _userSessionDal.GetActivePageAsync(userId, currentSessionId, page,
                pageSize, DateTime.UtcNow, cancellationToken);
            return new SuccessDataResult<PagedResult<UserSessionSummaryDto>>(result);
        }

        public async Task<IResult> RevokeOwnedSessionAsync(int userId, Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || sessionId == Guid.Empty)
                return new ErrorResult("Invalid session identifier.");

            IReadOnlyList<Guid> revokedIds;
            using (var transaction = new TransactionScope(TransactionScopeOption.Required,
                       new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted,
                           Timeout = TransactionManager.DefaultTimeout }, TransactionScopeAsyncFlowOption.Enabled))
            {
                await _unitOfWork.AcquireTransactionLockAsync(SessionFamilyLockBase + userId,
                    cancellationToken);
                revokedIds = await _userSessionDal.RevokeOwnedFamilyAsync(userId, sessionId,
                    DateTime.UtcNow, cancellationToken);
                transaction.Complete();
            }
            await EvictSessionCacheAsync(revokedIds);
            return new SuccessResult();
        }

        public async Task<IResult> RevokeOtherSessionsAsync(int userId, Guid currentSessionId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || currentSessionId == Guid.Empty)
                return new ErrorResult("Invalid session identifier.");

            IReadOnlyList<Guid> revokedIds;
            using (var transaction = new TransactionScope(TransactionScopeOption.Required,
                       new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted,
                           Timeout = TransactionManager.DefaultTimeout }, TransactionScopeAsyncFlowOption.Enabled))
            {
                await _unitOfWork.AcquireTransactionLockAsync(SessionFamilyLockBase + userId,
                    cancellationToken);
                revokedIds = await _userSessionDal.RevokeOtherActiveAsync(userId, currentSessionId,
                    DateTime.UtcNow, cancellationToken);
                transaction.Complete();
            }
            await EvictSessionCacheAsync(revokedIds);
            return new SuccessResult();
        }

        private async Task EvictSessionCacheAsync(IEnumerable<Guid> sessionIds)
        {
            var failedCount = 0;
            Exception? firstFailure = null;
            foreach (var id in sessionIds)
            {
                try
                {
                    await _distributedCache.RemoveAsync($"auth-session:{id}", CancellationToken.None);
                }
                catch (Exception exception)
                {
                    failedCount++;
                    firstFailure ??= exception;
                }
            }
            if (failedCount > 0)
                _logger.LogError(firstFailure, "Failed to evict {FailedCount} session validation cache entries.",
                    failedCount);
        }

        public Task<IResult> RevokeAllSessionsAsync(int userId, CancellationToken cancellationToken = default) =>
            _sessionInvalidationService.InvalidateUserSessionsAsync(userId, cancellationToken);

        private async Task<UserAuthenticationState> GetOrCreateAuthenticationStateAsync(int userId,
            CancellationToken cancellationToken)
        {
            var state = await _authenticationStateDal.GetAsync(item => item.UserId == userId, cancellationToken);
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
            await _authenticationStateDal.AddAsync(state, cancellationToken);
            return state;
        }

    }
}
