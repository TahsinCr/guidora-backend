using Core.Utilities.Results;

namespace Business.Abstract
{
    public interface IUserSecurityService
    {
        IResult Ban(int userId);
        IResult Restore(int userId);
        IResult InvalidateAuthorization(int userId);
    }
}
