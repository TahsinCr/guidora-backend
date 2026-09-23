using Core.Utilities.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Content;

public interface IProductResourceRules
{
    Task<IResult> EnsurePublishedAsync(int productId, CancellationToken cancellationToken = default);
    Task<IResult> EnsureDirectContentChangeAllowedAsync(int productId, CancellationToken cancellationToken = default);
}
