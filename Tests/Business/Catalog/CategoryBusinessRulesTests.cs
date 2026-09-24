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
    public async Task DetectsExistingSlugAndAllowsCurrentCategoryDuringUpdate()
    {
        var category = new Category { CategoryId = 4, Slug = "design" };
        var dal = new CategoryDalStub([category]);
        var rules = new CategoryBusinessRules(dal);

        Assert.False((await rules.EnsureSlugIsAvailableAsync("design")).Success);
        Assert.True((await rules.EnsureSlugIsAvailableAsync("design", excludingCategoryId: 4)).Success);
    }

    [Fact]
    public async Task DeactivateRetainsRowAndUsesUpdateInsteadOfPhysicalDelete()
    {
        var category = new Category { CategoryId = 4, IsActive = true };
        var dal = new CategoryDalStub([category]);
        var unitOfWork = new UnitOfWorkStub();
        var audit = new AuditLogDalStub();
        var manager = new CategoryManager(dal, unitOfWork, new CategoryBusinessRules(dal), audit);

        var result = await manager.DeactivateAsync(4, updatedByUserId: 15);

        Assert.True(result.Success);
        Assert.False(category.IsActive);
        Assert.Equal(1, dal.UpdateCount);
        Assert.Equal(0, dal.DeleteCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(15, Assert.Single(audit.Entries).ActorUserId);
    }

    private sealed class CategoryDalStub(IEnumerable<Category>? categories = null)
        : EntityRepositoryStub<Category>(categories), ICategoryDal;
}
