using Core.Utilities.Results;
using Entities;
using System.Collections.Generic;

namespace Business.BusinessRules.Catalog;

public interface ITagBusinessRules
{
    IResult EnsureSelectionIsValid(IEnumerable<Tag> selectedTags, IEnumerable<TagCategory> tagCategories);
}
