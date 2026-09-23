using Core.Utilities.Results;

namespace Business.BusinessRules.Catalog;

public interface ICategoryBusinessRules
{
    IResult EnsureSlugIsAvailable(string slug, int? excludingCategoryId = null);
}
