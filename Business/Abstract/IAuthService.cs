using Core.Entities;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Entities.DTOs;

namespace Business.Abstract;

public interface IAuthService
{
    IDataResult<User> Register(UserForRegisterDto userForRegisterDto, string password);
    IDataResult<User> Login(UserForLoginDto userForLoginDto);
    IResult UserExists(string email);
    IDataResult<AuthenticationResultDto> CreateSession(User user, bool rememberMe, string deviceName);
    IDataResult<AuthenticationResultDto> RefreshSession(string refreshToken, string deviceName);
    IResult RevokeSession(string refreshToken);
}
