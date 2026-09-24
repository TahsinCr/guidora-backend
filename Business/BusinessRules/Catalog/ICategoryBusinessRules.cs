using Core.Utilities.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Catalog;

public interface ICategoryBusinessRules
{
    Task<IResult> EnsureSlugIsAvailableAsync(string slug, int? excludingCategoryId = null,
        CancellationToken cancellationToken = default);
}
