using Business.BusinessRules.Catalog;
using Business.BusinessAspect.Autofac;
using Business.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;
using System.Collections.Generic;
using System.Reflection;

namespace Tests.Business.Catalog;

public sealed class CategoryBusinessRulesTests
{
    [Fact]
    public async Task ManagementCategoryReadIncludesInactiveRowsAndRequiresCategoryManageClaim()
    {
        var categories = new CategoryDalStub([
            new Category { CategoryId = 1, IsActive = true },
            new Category { CategoryId = 2, IsActive = false }]);
        var manager = new CategoryManager(categories, new UnitOfWorkStub(),
            new CategoryBusinessRules(categories), new AuditLogDalStub());

        var result = await manager.GetAllForManagementAsync();
        var method = typeof(CategoryManager).GetMethod(nameof(CategoryManager.GetAllForManagementAsync))!;
        var authorization = Assert.Single(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType == typeof(SecuredOperationAspect));
        var requiredClaims = Assert.IsAssignableFrom<IList<CustomAttributeTypedArgument>>(
            authorization.ConstructorArguments.Single().Value);

        Assert.True(result.Success);
        Assert.Equal([1, 2], result.Data!.Select(category => category.CategoryId).Order().ToArray());
        Assert.Equal("Category.Manage", Assert.Single(requiredClaims).Value);
    }

    [Fact]
    public async Task PublicCategoryReadsExcludeInactiveCategories()
    {
        var categories = new CategoryDalStub([
            new Category { CategoryId = 1, CategoryName = "Active", IsActive = true },
            new Category { CategoryId = 2, CategoryName = "Inactive", IsActive = false }]);
        var manager = new CategoryManager(categories, new UnitOfWorkStub(),
            new CategoryBusinessRules(categories), new AuditLogDalStub());

        var list = await manager.GetAllAsync();
        var active = await manager.GetByIdAsync(1);
        var inactive = await manager.GetByIdAsync(2);

        Assert.True(list.Success);
        Assert.Equal([1], list.Data!.Select(category => category.CategoryId));
        Assert.True(active.Success);
        Assert.Equal(1, active.Data!.CategoryId);
        Assert.False(inactive.Success);
    }

    [Fact]
    public async Task DetectsExistingSlugAndAllowsCurrentCategoryDuringUpdate()
    {
        var category = new Category { CategoryId = 4, Slug = "design" };
        var dal = new CategoryDalStub([category]);
        var rules = new CategoryBusinessRules(dal);

        var duplicateSlug = await rules.EnsureSlugIsAvailableAsync("design");
        Assert.IsAssignableFrom<IConflictResult>(duplicateSlug);
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
