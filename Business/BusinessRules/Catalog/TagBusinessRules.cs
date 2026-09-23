using Core.Utilities.Results;
using Entities;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Business.BusinessRules.Catalog;

public sealed class TagBusinessRules : ITagBusinessRules
{
    public IResult EnsureSelectionIsValid(IEnumerable<Tag> selectedTags, IEnumerable<TagCategory> tagCategories)
    {
        ArgumentNullException.ThrowIfNull(selectedTags);
        ArgumentNullException.ThrowIfNull(tagCategories);

        var tags = selectedTags.ToArray();
        var categories = tagCategories.Where(category => category.IsActive).ToDictionary(category => category.Id);

        if (tags.Any(tag => !tag.IsActive || !categories.ContainsKey(tag.TagCategoryId)))
        {
            return new ErrorResult("Selected tags or their categories are inactive or unavailable.");
        }

        foreach (var group in tags.GroupBy(tag => tag.TagCategoryId))
        {
            var category = categories[group.Key];
            var selectedInCategory = group.ToArray();
            if (!category.AllowsMultipleSelection && selectedInCategory.Length > 1)
            {
                return new ErrorResult("Only one tag may be selected from this category.");
            }

            if (selectedInCategory.Any(tag => tag.IsExclusive) && selectedInCategory.Length > 1)
            {
                return new ErrorResult("An exclusive tag cannot be combined with another tag in its category.");
            }
        }

        return new SuccessResult();
    }
}
