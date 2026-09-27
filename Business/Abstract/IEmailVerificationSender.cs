using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IEmailVerificationSender
{
    Task SendAsync(string address, string token, CancellationToken cancellationToken = default);
}
