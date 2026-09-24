using Core.Utilities.Results;

namespace Business.Abstract
{
    using System.Collections.Generic;

    public interface IUserSessionInvalidationService
    {
        IResult InvalidateUserSessions(int userId);
        IResult InvalidateUsersSessions(IReadOnlyCollection<int> userIds);
    }
}
