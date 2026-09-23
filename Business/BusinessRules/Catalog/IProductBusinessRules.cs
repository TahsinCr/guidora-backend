using Core.Utilities.Results;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Catalog;

public interface IProductBusinessRules
{
    IResult EnsureNameIsAvailable(string name, int? excludingProductId = null);
    IResult EnsureCategoriesAreActive(IEnumerable<int> categoryIds);
    Task<IResult> EnsureNameIsAvailableAsync(string name, int? excludingProductId = null,
        CancellationToken cancellationToken = default);
    Task<IResult> EnsureCategoriesAreActiveAsync(IEnumerable<int> categoryIds,
        CancellationToken cancellationToken = default);
}
