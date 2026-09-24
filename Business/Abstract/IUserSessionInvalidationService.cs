using Core.Utilities.Results;

namespace Business.Abstract
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IUserSessionInvalidationService
    {
        Task<IResult> InvalidateUserSessionsAsync(int userId, CancellationToken cancellationToken = default);
        Task<IResult> InvalidateUsersSessionsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);
    }
}
