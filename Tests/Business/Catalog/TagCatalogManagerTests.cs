using Business.Concrete;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Catalog;

public sealed class TagCatalogManagerTests
{
    [Fact]
    public async Task PublicTagCatalogReturnsOnlyTagsFromActiveCategories()
    {
        var categories = new TagCategoryDalStub([
            new TagCategory { Id = 1, IsActive = true },
            new TagCategory { Id = 2, IsActive = false }]);
        var tags = new TagDalStub([
            new Tag { Id = 11, TagCategoryId = 1, IsActive = true },
            new Tag { Id = 12, TagCategoryId = 1, IsActive = false },
            new Tag { Id = 21, TagCategoryId = 2, IsActive = true }]);
        var manager = new TagManager(tags, categories, new ProductTagDalStub(), new CommunityAuditDalStub(), new UnitOfWorkStub());

        var result = await manager.GetActiveAsync();

        Assert.True(result.Success);
        Assert.Equal([11], result.Data!.Select(item => item.Id));
    }

    [Fact]
    public async Task DuplicateTagSlugReturnsConflictWithoutWriting()
    {
        var tags = new TagDalStub([new Tag { Id = 7, TagCategoryId = 4, Slug = "linux" }]);
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new TagManager(tags, new TagCategoryDalStub([
            new TagCategory { Id = 4, IsActive = true }
        ]), new ProductTagDalStub(), audit, unitOfWork);

        var result = await manager.CreateAsync(new TagRequest
        {
            TagCategoryId = 4,
            Name = "Linux",
            Slug = "linux"
        }, actorUserId: 99);

        Assert.IsAssignableFrom<IConflictResult>(result);
        Assert.Single(tags.GetAll());
        Assert.Empty(audit.Entries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeactivatingTagCategoryAlsoDeactivatesItsTagsAndAuditsTheChange()
    {
        var category = new TagCategory { Id = 4, Name = "Platform", Slug = "platform", IsActive = true };
        var categories = new TagCategoryDalStub([category]);
        var tag = new Tag { Id = 8, TagCategoryId = 4, Name = "Linux", Slug = "linux", IsActive = true };
        var tags = new TagDalStub([tag]);
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new TagCategoryManager(categories, tags, new ProductTagDalStub(), audit, unitOfWork);

        var result = await manager.DeactivateAsync(4, actorUserId: 99);

        Assert.True(result.Success);
        Assert.False(category.IsActive);
        Assert.False(tag.IsActive);
        Assert.Equal("Deactivated", Assert.Single(audit.Entries).Action);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task MovingProductAssignedTagIsRejectedWithoutMutation()
    {
        var tags = new TagDalStub([new Tag { Id = 8, TagCategoryId = 4, Name = "Linux", Slug = "linux" }]);
        var categories = new TagCategoryDalStub([
            new TagCategory { Id = 4, IsActive = true }, new TagCategory { Id = 5, IsActive = true }]);
        var audit = new CommunityAuditDalStub();
        var manager = new TagManager(tags, categories,
            new ProductTagDalStub([new ProductTag { ProductId = 2, TagId = 8 }]), audit, new UnitOfWorkStub());

        var result = await manager.UpdateAsync(8,
            new TagRequest { TagCategoryId = 5, Name = "Linux", Slug = "linux" }, actorUserId: 99);

        Assert.False(result.Success);
        Assert.Equal(4, Assert.Single(tags.GetAll()).TagCategoryId);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task MakingProductAssignedTagExclusiveIsRejectedWithoutMutation()
    {
        var tag = new Tag { Id = 8, TagCategoryId = 4, Name = "Linux", Slug = "linux", IsExclusive = false };
        var tags = new TagDalStub([tag]);
        var categories = new TagCategoryDalStub([new TagCategory { Id = 4, IsActive = true }]);
        var assignments = new ProductTagDalStub([new ProductTag { ProductId = 2, TagId = 8 }])
        {
            HasConflictingSameCategoryTag = true
        };
        var audit = new CommunityAuditDalStub();
        var manager = new TagManager(tags, categories, assignments, audit, new UnitOfWorkStub());

        var result = await manager.UpdateAsync(8,
            new TagRequest
            {
                TagCategoryId = 4,
                Name = "Linux",
                Slug = "linux",
                IsExclusive = true
            }, actorUserId: 99);

        Assert.IsAssignableFrom<IConflictResult>(result);
        Assert.False(tag.IsExclusive);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task MakingAssignedTagExclusiveIsAllowedWhenNoProductHasAnotherTagInItsCategory()
    {
        var tag = new Tag { Id = 8, TagCategoryId = 4, Name = "Linux", Slug = "linux", IsExclusive = false };
        var tags = new TagDalStub([tag]);
        var categories = new TagCategoryDalStub([new TagCategory { Id = 4, IsActive = true }]);
        var assignments = new ProductTagDalStub([new ProductTag { ProductId = 2, TagId = 8 }]);
        var audit = new CommunityAuditDalStub();
        var manager = new TagManager(tags, categories, assignments, audit, new UnitOfWorkStub());

        var result = await manager.UpdateAsync(8,
            new TagRequest
            {
                TagCategoryId = 4,
                Name = "Linux",
                Slug = "linux",
                IsExclusive = true
            }, actorUserId: 99);

        Assert.True(result.Success);
        Assert.True(tag.IsExclusive);
        Assert.Equal("Updated", Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task SelectionModeCannotBecomeSingleWhileProductsHaveMultipleCategoryTags()
    {
        var category = new TagCategory { Id = 4, Name = "Platforms", Slug = "platforms", AllowsMultipleSelection = true };
        var tags = new TagDalStub([
            new Tag { Id = 8, TagCategoryId = 4 }, new Tag { Id = 9, TagCategoryId = 4 }]);
        var assignments = new ProductTagDalStub([
            new ProductTag { ProductId = 2, TagId = 8 }, new ProductTag { ProductId = 2, TagId = 9 }]);
        assignments.HasMultipleTagsInCategory = true;
        var manager = new TagCategoryManager(new TagCategoryDalStub([category]), tags, assignments,
            new CommunityAuditDalStub(), new UnitOfWorkStub());

        var result = await manager.UpdateAsync(4,
            new TagCategoryRequest { Name = "Platforms", Slug = "platforms", AllowsMultipleSelection = false },
            actorUserId: 99);

        Assert.False(result.Success);
        Assert.True(category.AllowsMultipleSelection);
    }

    [Fact]
    public async Task DuplicateTagCategorySlugReturnsConflictWithoutWriting()
    {
        var categories = new TagCategoryDalStub([
            new TagCategory { Id = 4, Slug = "platforms" }
        ]);
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new TagCategoryManager(categories, new TagDalStub(), new ProductTagDalStub(), audit, unitOfWork);

        var result = await manager.CreateAsync(new TagCategoryRequest
        {
            Name = "Platforms",
            Slug = "platforms",
            AllowsMultipleSelection = true
        }, actorUserId: 99);

        Assert.IsAssignableFrom<IConflictResult>(result);
        Assert.Single(categories.GetAll());
        Assert.Empty(audit.Entries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private sealed class TagDalStub(IEnumerable<Tag>? values = null) : EntityRepositoryStub<Tag>(values), ITagDal;
    private sealed class TagCategoryDalStub(IEnumerable<TagCategory>? values = null)
        : EntityRepositoryStub<TagCategory>(values), ITagCategoryDal;
    private sealed class ProductTagDalStub(IEnumerable<ProductTag>? values = null)
        : EntityRepositoryStub<ProductTag>(values), IProductTagDal
    {
        public bool HasConflictingSameCategoryTag { get; init; }
        public bool HasMultipleTagsInCategory { get; set; }

        public Task<bool> HasOtherTagInSameCategoryForAssignedProductsAsync(int tagId, int tagCategoryId,
            CancellationToken cancellationToken = default) => Task.FromResult(HasConflictingSameCategoryTag);

        public Task<bool> HasProductsWithMultipleTagsInCategoryAsync(int tagCategoryId,
            CancellationToken cancellationToken = default) => Task.FromResult(HasMultipleTagsInCategory);
    }
}
