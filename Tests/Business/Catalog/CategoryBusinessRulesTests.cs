using Business.BusinessRules.Catalog;
using Business.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Catalog;

public sealed class CategoryBusinessRulesTests
{
    [Fact]
    public void DetectsExistingSlugAndAllowsCurrentCategoryDuringUpdate()
    {
        var category = new Category { CategoryId = 4, Slug = "design" };
        var dal = new CategoryDalStub([category]);
        var rules = new CategoryBusinessRules(dal);

        Assert.False(rules.EnsureSlugIsAvailable("design").Success);
        Assert.True(rules.EnsureSlugIsAvailable("design", excludingCategoryId: 4).Success);
    }

    [Fact]
    public void DeactivateRetainsRowAndUsesUpdateInsteadOfPhysicalDelete()
    {
        var category = new Category { CategoryId = 4, IsActive = true };
        var dal = new CategoryDalStub([category]);
        var unitOfWork = new UnitOfWorkStub();
        var manager = new CategoryManager(dal, unitOfWork, new CategoryBusinessRules(dal));

        var result = manager.Deactivate(4);

        Assert.True(result.Success);
        Assert.False(category.IsActive);
        Assert.Equal(1, dal.UpdateCount);
        Assert.Equal(0, dal.DeleteCount);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class CategoryDalStub(IEnumerable<Category>? categories = null)
        : EntityRepositoryStub<Category>(categories), ICategoryDal;
}
