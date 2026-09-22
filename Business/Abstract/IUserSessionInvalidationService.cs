using Core.Utilities.Results;

namespace Business.Abstract
{
    public interface IUserSessionInvalidationService
    {
        IResult InvalidateUserSessions(int userId);
    }
}
