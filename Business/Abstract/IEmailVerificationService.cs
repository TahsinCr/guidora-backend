using System.Threading;
using System.Threading.Tasks;
using Core.Utilities.Results;

namespace Business.Abstract;

public interface IEmailVerificationService
{
    Task<IResult> RequestAsync(int userId, CancellationToken cancellationToken = default);
    Task<IResult> ConfirmAsync(int userId, string token, CancellationToken cancellationToken = default);
}
