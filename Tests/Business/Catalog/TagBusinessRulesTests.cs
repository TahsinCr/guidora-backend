using Business.BusinessRules.Catalog;
using Entities;

namespace Tests.Business.Catalog;

public sealed class TagBusinessRulesTests
{
    private readonly TagBusinessRules _rules = new();

    [Fact]
    public void AllowsMultipleSelectionWhenCategoryPermitsIt()
    {
        var result = _rules.EnsureSelectionIsValid(
            [new Tag { Id = 1, TagCategoryId = 10, IsActive = true }, new Tag { Id = 2, TagCategoryId = 10, IsActive = true }],
            [new TagCategory { Id = 10, IsActive = true, AllowsMultipleSelection = true }]);

        Assert.True(result.Success);
    }

    [Fact]
    public void RejectsMultipleSelectionForSingleSelectCategory()
    {
        var result = _rules.EnsureSelectionIsValid(
            [new Tag { Id = 1, TagCategoryId = 10, IsActive = true }, new Tag { Id = 2, TagCategoryId = 10, IsActive = true }],
            [new TagCategory { Id = 10, IsActive = true, AllowsMultipleSelection = false }]);

        Assert.False(result.Success);
    }

    [Fact]
    public void RejectsExclusiveTagCombinedWithAnotherTag()
    {
        var result = _rules.EnsureSelectionIsValid(
            [new Tag { Id = 1, TagCategoryId = 10, IsActive = true, IsExclusive = true }, new Tag { Id = 2, TagCategoryId = 10, IsActive = true }],
            [new TagCategory { Id = 10, IsActive = true, AllowsMultipleSelection = true }]);

        Assert.False(result.Success);
    }

    [Fact]
    public void RejectsInactiveTagsAndInactiveCategories()
    {
        var inactiveTag = _rules.EnsureSelectionIsValid(
            [new Tag { Id = 1, TagCategoryId = 10, IsActive = false }],
            [new TagCategory { Id = 10, IsActive = true, AllowsMultipleSelection = true }]);
        var inactiveCategory = _rules.EnsureSelectionIsValid(
            [new Tag { Id = 1, TagCategoryId = 10, IsActive = true }],
            [new TagCategory { Id = 10, IsActive = false, AllowsMultipleSelection = true }]);

        Assert.False(inactiveTag.Success);
        Assert.False(inactiveCategory.Success);
    }
}
