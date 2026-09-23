using Core.Utilities.Results;
using System.Collections.Generic;

namespace Business.BusinessRules.Catalog;

public interface IProductBusinessRules
{
    IResult EnsureNameIsAvailable(string name, int? excludingProductId = null);
    IResult EnsureCategoriesAreActive(IEnumerable<int> categoryIds);
}
