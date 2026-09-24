using Core.Utilities.Results;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Catalog;

public interface IProductBusinessRules
{
    Task<IResult> EnsureIdentityIsAvailableAsync(string name, string slug, string websiteUrl,
        int? excludingProductId = null, CancellationToken cancellationToken = default);
    Task<IResult> EnsureCategoriesAreActiveAsync(IEnumerable<int> categoryIds,
        CancellationToken cancellationToken = default);
}
