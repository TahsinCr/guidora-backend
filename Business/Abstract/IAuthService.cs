using Core.Entities;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Business.Abstract;

public interface IAuthService
{
    Task<IDataResult<User>> RegisterAsync(UserForRegisterDto userForRegisterDto, string password, CancellationToken cancellationToken = default);
    Task<IDataResult<User>> LoginAsync(UserForLoginDto userForLoginDto, CancellationToken cancellationToken = default);
    Task<IResult> UserExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<IDataResult<AuthenticationResultDto>> CreateSessionAsync(User user, bool rememberMe, string deviceName, CancellationToken cancellationToken = default);
    Task<IDataResult<AuthenticationResultDto>> RefreshSessionAsync(string refreshToken, string deviceName, CancellationToken cancellationToken = default);
    Task<IResult> RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<IDataResult<PagedResult<UserSessionSummaryDto>>> GetSessionsAsync(int userId,
        Guid currentSessionId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IResult> RevokeOwnedSessionAsync(int userId, Guid sessionId,
        CancellationToken cancellationToken = default);
    Task<IResult> RevokeOtherSessionsAsync(int userId, Guid currentSessionId,
        CancellationToken cancellationToken = default);
    Task<IResult> RevokeAllSessionsAsync(int userId, CancellationToken cancellationToken = default);
}
