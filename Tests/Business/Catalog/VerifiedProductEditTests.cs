using Business.BusinessRules.Catalog;
using Business.Concrete;
using Business.BusinessRules.Moderation;
using Business.Constants;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Catalog;

public sealed class VerifiedProductEditTests
{
    [Fact]
    public async Task VerifiedProductEditCreatesScalarAndRelationshipSnapshotWithoutChangingCanonicalProduct()
    {
        var product = new Product { ProductId = 30, Name = "Original", Slug = "original", ProductStatusId = 1 };
        var productDal = new ProductDalStub([product]);
        var categoryDal = new CategoryDalStub([new Category { CategoryId = 4, IsActive = true }]);
        var proposalDal = new ProductEditProposalDalStub();
        var proposalCategoryDal = new ProposalCategoryDalStub();
        var proposalTagDal = new ProposalTagDalStub();
        var manager = CreateManager(productDal, categoryDal,
            new VerificationDalStub([new ProductVerification { ProductId = 30, VerificationTypeId = 1 }]),
            new ProductTagDalStub([new ProductTag { ProductId = 30, TagId = 12 }]),
            proposalDal, proposalCategoryDal, proposalTagDal, out var unitOfWork, out var auditLogDal);

        var request = Request("new-name", [4]);
        request.TagIds = [13];
        var result = await manager.UpdateAsync(30, request, updatedByUserId: 99);

        Assert.True(result.Success);
        Assert.IsNotAssignableFrom<ICacheInvalidationDirective>(result);
        Assert.Equal("Original", product.Name);
        Assert.Equal(0, productDal.UpdateCount);
        var proposal = Assert.Single(proposalDal.GetAll());
        Assert.Equal(ProductEditProposalStatus.Pending, proposal.Status);
        Assert.Equal(99, proposal.ProposedByUserId);
        Assert.Equal("new-name", proposal.Name);
        var proposedCategory = Assert.Single(proposalCategoryDal.GetAll());
        Assert.Equal(501, proposedCategory.ProductEditProposalId);
        Assert.Equal(13, proposalTagDal.GetAll().Single().TagId);
        var proposalAudit = Assert.Single(auditLogDal.Entries);
        Assert.Equal("Submitted", proposalAudit.Action);
        Assert.Equal(99, proposalAudit.ActorUserId);
        Assert.Equal(2, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnverifiedProductEditUpdatesCanonicalProductAndDoesNotCreateProposal()
    {
        var product = new Product { ProductId = 30, Name = "Original", Slug = "original", ProductStatusId = 1 };
        var productDal = new ProductDalStub([product]);
        var oldLink = new ProductCategory { ProductId = 30, CategoryId = 2 };
        var productCategoryDal = new ProductCategoryDalStub([oldLink]);
        var productTagDal = new ProductTagDalStub();
        var proposalDal = new ProductEditProposalDalStub();
        var manager = CreateManager(productDal, new CategoryDalStub([new Category { CategoryId = 4, IsActive = true }]),
            new VerificationDalStub(), productTagDal, proposalDal, new ProposalCategoryDalStub(),
            new ProposalTagDalStub(), out var unitOfWork, out var auditLogDal, productCategoryDal);

        var result = await manager.UpdateAsync(30, Request("new-name", [4]), updatedByUserId: 99);

        Assert.True(result.Success);
        Assert.Equal(Messages.ProductUpdated, result.Message);
        var invalidation = Assert.IsAssignableFrom<ICacheInvalidationDirective>(result);
        Assert.Collection(invalidation.CacheInvalidations,
            target => Assert.Equal(CacheProfiles.CatalogListing, target.Profile),
            target =>
            {
                Assert.Equal(CacheProfiles.ProductDetail, target.Profile);
                Assert.Equal(0, target.ArgumentIndex);
                Assert.Equal(30, target.ArgumentValue);
            });
        Assert.Equal("new-name", product.Name);
        Assert.Equal(1, productDal.UpdateCount);
        Assert.Empty(proposalDal.GetAll());
        Assert.Equal(1, productCategoryDal.DeleteCount);
        var currentCategory = Assert.Single(productCategoryDal.GetAll());
        Assert.Equal(4, currentCategory.CategoryId);
        Assert.Equal(12, Assert.Single(productTagDal.GetAll()).TagId);
        Assert.Equal("Updated", Assert.Single(auditLogDal.Entries).Action);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private static ProductManager CreateManager(ProductDalStub productDal, ICategoryDal categoryDal,
        IProductVerificationDal verificationDal, IProductTagDal productTagDal,
        ProductEditProposalDalStub proposalDal, ProposalCategoryDalStub proposalCategoryDal,
        ProposalTagDalStub proposalTagDal, out UnitOfWorkStub unitOfWork,
        out AuditLogDalStub auditLogDal,
        ProductCategoryDalStub? productCategoryDal = null)
    {
        unitOfWork = new UnitOfWorkStub();
        auditLogDal = new AuditLogDalStub();
        return new ProductManager(productDal, productCategoryDal ?? new ProductCategoryDalStub(),
            new ProductStatusDalStub(), productTagDal,
            new TagDalStub([
                new Tag { Id = 12, TagCategoryId = 3, IsActive = true },
                new Tag { Id = 13, TagCategoryId = 3, IsActive = true }
            ]),
            new TagCategoryDalStub([new TagCategory { Id = 3, IsActive = true, AllowsMultipleSelection = true }]),
            verificationDal, proposalDal, proposalTagDal,
            proposalCategoryDal, auditLogDal, unitOfWork,
            new ProductBusinessRules(productDal, categoryDal), new TagBusinessRules(),
            new DeterministicContentModerationRule());
    }

    private static UpdateProductRequest Request(string name, List<int> categoryIds) => new()
    {
        Name = name,
        Slug = name,
        ShortDescription = "short",
        Description = "details",
        WebsiteUrl = "https://example.test",
        LogoUrl = "https://example.test/logo.png",
        CategoryIds = categoryIds,
        TagIds = [12]
    };

    private sealed class ProductDalStub(IEnumerable<Product>? values = null) : EntityRepositoryStub<Product>(values), IProductDal
    {
        public Task<List<Product>> GetPublishedPageAsync(int statusId, int? categoryId, int skip, int take,
            CancellationToken cancellationToken = default) => Task.FromResult(categoryId.HasValue ? [] :
            GetAll(item => item.ProductStatusId == statusId).OrderBy(item => item.Name).Skip(skip).Take(take).ToList());
        public Task<int> CountPublishedAsync(int statusId, int? categoryId,
            CancellationToken cancellationToken = default) => Task.FromResult(categoryId.HasValue ? 0 :
            GetAll(item => item.ProductStatusId == statusId).Count);
    }
    private sealed class ProductStatusDalStub(IEnumerable<ProductStatus>? values = null) : EntityRepositoryStub<ProductStatus>(values), IProductStatusDal;
    private sealed class ProductCategoryDalStub(IEnumerable<ProductCategory>? values = null)
        : EntityRepositoryStub<ProductCategory>(values), IProductCategoryDal
    {
        public override void PurgeRange(IEnumerable<ProductCategory> entities)
        {
            foreach (var entity in entities.ToArray()) Purge(entity);
        }

        public override void Purge(ProductCategory entity)
        {
            Entities.Remove(entity);
            base.Purge(entity);
        }
    }
    private sealed class ProductTagDalStub(IEnumerable<ProductTag>? values = null)
        : EntityRepositoryStub<ProductTag>(values), IProductTagDal
    {
        public override void PurgeRange(IEnumerable<ProductTag> entities)
        {
            foreach (var entity in entities.ToArray()) Purge(entity);
        }
        public override void Purge(ProductTag entity)
        {
            Entities.Remove(entity);
            base.Purge(entity);
        }
    }
    private sealed class TagDalStub(IEnumerable<Tag>? values = null) : EntityRepositoryStub<Tag>(values), ITagDal;
    private sealed class TagCategoryDalStub(IEnumerable<TagCategory>? values = null) : EntityRepositoryStub<TagCategory>(values), ITagCategoryDal;
    private sealed class VerificationDalStub(IEnumerable<ProductVerification>? values = null) : EntityRepositoryStub<ProductVerification>(values), IProductVerificationDal;
    private sealed class CategoryDalStub(IEnumerable<Category>? values = null) : EntityRepositoryStub<Category>(values), ICategoryDal;

    private sealed class ProductEditProposalDalStub : EntityRepositoryStub<ProductEditProposal>, IProductEditProposalDal
    {
        public override void Add(ProductEditProposal entity)
        {
            entity.Id = 501;
            base.Add(entity);
        }
    }

    private sealed class ProposalCategoryDalStub(IEnumerable<ProductEditProposalCategory>? values = null)
        : EntityRepositoryStub<ProductEditProposalCategory>(values), IProductEditProposalCategoryDal;
    private sealed class ProposalTagDalStub(IEnumerable<ProductEditProposalTag>? values = null)
        : EntityRepositoryStub<ProductEditProposalTag>(values), IProductEditProposalTagDal;
}
