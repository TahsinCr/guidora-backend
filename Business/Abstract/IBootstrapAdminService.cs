using System.Threading;
using System.Threading.Tasks;
using Core.Utilities.Results;

namespace Business.Abstract;

public interface IBootstrapAdminService
{
    Task<IResult> EnsureCreatedAsync(string username, string email, string password,
        CancellationToken cancellationToken = default);
}
