using Core.Utilities.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IUserSecurityService
    {
        Task<IResult> BanAsync(int userId, CancellationToken cancellationToken = default);
        Task<IResult> RestoreAsync(int userId, CancellationToken cancellationToken = default);
        Task<IResult> InvalidateAuthorizationAsync(int userId, CancellationToken cancellationToken = default);
    }
}
