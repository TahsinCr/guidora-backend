using Core.Utilities.Results;

namespace Business.Abstract
{
    public interface ITokenValidationService
    {
        IResult Validate(int userId, string sessionId, int authorizationVersion);
    }
}
