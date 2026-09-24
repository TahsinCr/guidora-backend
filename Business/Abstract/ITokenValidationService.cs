using Core.Utilities.Results;

namespace Business.Abstract
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITokenValidationService
    {
        Task<IResult> ValidateAsync(int userId, string sessionId, int authorizationVersion,
            CancellationToken cancellationToken = default);
    }
}
