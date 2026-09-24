using Business.BusinessRules.Catalog;
using Business.BusinessRules.Moderation;
using Business.Concrete;
using Business.Utilities;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Catalog;

public sealed class ProductBusinessRulesTests
{
    [Fact]
    public async Task RejectsDuplicateNameAndSlugButAllowsCurrentProductDuringUpdate()
    {
        var product = new Product { ProductId = 8, Name = "Guidora", Slug = "guidora" };
        var rules = new ProductBusinessRules(new ProductDalStub([product]), new CategoryDalStub());

        Assert.False((await rules.EnsureIdentityIsAvailableAsync(
            "guidora", "new-slug", "https://different.example.test")).Success);
        Assert.False((await rules.EnsureIdentityIsAvailableAsync(
            "A new product", "GUIDORA", "https://different.example.test")).Success);
        Assert.True((await rules.EnsureIdentityIsAvailableAsync(
            "guidora", "guidora", "https://different.example.test", product.ProductId)).Success);
    }

    [Fact]
    public async Task RequiresAtLeastOneExistingActiveCategory()
    {
        var categories = new CategoryDalStub([
            new Category { CategoryId = 1, IsActive = true },
            new Category { CategoryId = 2, IsActive = false }]);
        var rules = new ProductBusinessRules(new ProductDalStub(), categories);

        Assert.False((await rules.EnsureCategoriesAreActiveAsync([])).Success);
        Assert.True((await rules.EnsureCategoriesAreActiveAsync([1, 1])).Success);
        Assert.False((await rules.EnsureCategoriesAreActiveAsync([1, 2])).Success);
        Assert.False((await rules.EnsureCategoriesAreActiveAsync([1, 99])).Success);
    }

    [Fact]
    public async Task ProductIdentityCheckIsCaseInsensitiveAndCanonicalizesWebAddresses()
    {
        var existing = new Product
        {
            ProductId = 8,
            Name = "Guidora",
            Slug = "guidora",
            WebsiteUrl = ProductIdentityNormalizer.NormalizeWebsiteUrl("https://example.test/product")
        };
        var rules = new ProductBusinessRules(new ProductDalStub([existing]), new CategoryDalStub());

        Assert.False((await rules.EnsureIdentityIsAvailableAsync(
            "Other", "GUIDORA", "https://another.test")).Success);
        Assert.False((await rules.EnsureIdentityIsAvailableAsync(
            " guidora ", "another", "https://another.test")).Success);
        Assert.True((await rules.EnsureIdentityIsAvailableAsync(
            "Other", "another", "https://another.test", excludingProductId: 8)).Success);
        Assert.True((await rules.EnsureIdentityIsAvailableAsync(
            "Another Product", "another-product",
            "HTTPS://EXAMPLE.TEST/product/?utm_source=guidora")).Success);
    }

    [Fact]
    public void WebsiteUrlNormalizationDropsTrackingAndNormalizesAuthorityAndPath()
    {
        var normalized = ProductIdentityNormalizer.NormalizeWebsiteUrl(
            "HTTPS://Example.TEST:443/app/?utm_source=campaign&b=2&a=1#section");

        Assert.Equal("https://example.test/app?a=1&b=2", normalized);
    }

    [Fact]
    public void PublicProductQueriesExcludeHiddenAndDeletedStatuses()
    {
        var productDal = new ProductDalStub([
            new Product { ProductId = 1, ProductStatusId = 1 },
            new Product { ProductId = 2, ProductStatusId = 2 },
            new Product { ProductId = 3, ProductStatusId = 3 }
        ]);
        var manager = CreateProductManager(productDal,
            new ProductStatusDalStub([
                new ProductStatus { Id = 1, Code = "published" },
                new ProductStatus { Id = 2, Code = "hidden" },
                new ProductStatus { Id = 3, Code = "deleted" }
            ]), new ProductCategoryDalStub([
                new ProductCategory { ProductId = 1, CategoryId = 7 },
                new ProductCategory { ProductId = 2, CategoryId = 7 },
                new ProductCategory { ProductId = 3, CategoryId = 7 }
            ]));

        Assert.Equal([1], manager.GetAll().Data!.Select(product => product.ProductId));
        Assert.Equal([1], manager.GetAllByCategoryId(7).Data!.Select(product => product.ProductId));
        Assert.True(manager.GetById(1).Success);
        Assert.False(manager.GetById(2).Success);
        Assert.False(manager.GetById(3).Success);
    }

    [Fact]
    public async Task ProductCreationRejectsModeratedTextBeforeAnyPersistence()
    {
        var products = new ProductDalStub();
        var manager = CreateProductManager(products,
            new ProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new ProductCategoryDalStub());

        var result = await manager.CreateAsync(new CreateProductRequest
        {
            Name = "A normal product name",
            Slug = "normal-product",
            WebsiteUrl = "https://normal-product.example.test",
            ShortDescription = "f.u.c.k",
            CategoryIds = [1]
        }, createdByUserId: 12);

        Assert.False(result.Success);
        Assert.Empty(products.GetAll());
    }

    [Fact]
    public async Task ProductCreatePersistsRequestedClassificationAndAuditActor()
    {
        var productDal = new ProductDalStub();
        var categories = new CategoryDalStub([new Category { CategoryId = 1, IsActive = true }]);
        var productCategories = new ProductCategoryDalStub();
        var productTags = new ProductTagDalStub();
        var audit = new AuditLogDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new ProductManager(productDal, productCategories,
            new ProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]), productTags,
            new TagDalStub([new Tag { Id = 2, TagCategoryId = 3, IsActive = true }]),
            new TagCategoryDalStub([new TagCategory { Id = 3, IsActive = true, AllowsMultipleSelection = true }]),
            new ProductVerificationDalStub(), new ProductEditProposalDalStub(),
            new ProductEditProposalTagDalStub(), new ProductEditProposalCategoryDalStub(), audit, unitOfWork,
            new ProductBusinessRules(productDal, categories), new TagBusinessRules(),
            new DeterministicContentModerationRule());

        var result = await manager.CreateAsync(new CreateProductRequest
        {
            Name = "Guidora", Slug = "guidora", WebsiteUrl = "https://guidora.example.test",
            CategoryIds = [1], TagIds = [2]
        }, createdByUserId: 12);

        Assert.True(result.Success);
        var product = Assert.Single(productDal.GetAll());
        Assert.Equal(12, product.CreatedByUserId);
        Assert.Equal(1, Assert.Single(productCategories.GetAll()).CategoryId);
        Assert.Equal(2, Assert.Single(productTags.GetAll()).TagId);
        Assert.Equal(12, Assert.Single(audit.Entries).ActorUserId);
        Assert.Equal(2, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProductDeleteCommandChangesStatusWithoutPhysicalDeletion()
    {
        var product = new Product { ProductId = 8, ProductStatusId = 1 };
        var productDal = new ProductDalStub([product]);
        var categoryDal = new CategoryDalStub();
        var statusDal = new ProductStatusDalStub([new ProductStatus { Id = 3, Code = "deleted" }]);
        var unitOfWork = new UnitOfWorkStub();
        var auditLogDal = new AuditLogDalStub();
        var manager = new ProductManager(productDal, new ProductCategoryDalStub(), statusDal,
            new ProductTagDalStub(), new TagDalStub(), new TagCategoryDalStub(),
            new ProductVerificationDalStub(), new ProductEditProposalDalStub(),
            new ProductEditProposalTagDalStub(), new ProductEditProposalCategoryDalStub(), auditLogDal, unitOfWork,
            new ProductBusinessRules(productDal, categoryDal), new TagBusinessRules(),
            new DeterministicContentModerationRule());

        var result = await manager.DeleteAsync(product.ProductId, deletedByUserId: 12);

        Assert.True(result.Success);
        Assert.Equal(3, product.ProductStatusId);
        Assert.Equal(1, productDal.UpdateCount);
        Assert.Equal(0, productDal.DeleteCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(12, Assert.Single(auditLogDal.Entries).ActorUserId);
    }

    private sealed class ProductDalStub(IEnumerable<Product>? products = null)
        : EntityRepositoryStub<Product>(products), IProductDal
    {
        public override void Add(Product entity)
        {
            entity.ProductId = 42;
            base.Add(entity);
        }
    }

    private static ProductManager CreateProductManager(ProductDalStub products, ProductStatusDalStub statuses,
        ProductCategoryDalStub categories) => new(products, categories, statuses, new ProductTagDalStub(),
        new TagDalStub(), new TagCategoryDalStub(), new ProductVerificationDalStub(),
        new ProductEditProposalDalStub(), new ProductEditProposalTagDalStub(),
        new ProductEditProposalCategoryDalStub(), new AuditLogDalStub(), new UnitOfWorkStub(),
        new ProductBusinessRules(products, new CategoryDalStub()), new TagBusinessRules(),
        new DeterministicContentModerationRule());
    private sealed class CategoryDalStub(IEnumerable<Category>? categories = null) : EntityRepositoryStub<Category>(categories), ICategoryDal;
    private sealed class ProductStatusDalStub(IEnumerable<ProductStatus>? statuses = null) : EntityRepositoryStub<ProductStatus>(statuses), IProductStatusDal;
    private sealed class ProductCategoryDalStub(IEnumerable<ProductCategory>? links = null) : EntityRepositoryStub<ProductCategory>(links), IProductCategoryDal;
    private sealed class ProductTagDalStub(IEnumerable<ProductTag>? links = null) : EntityRepositoryStub<ProductTag>(links), IProductTagDal;
    private sealed class TagDalStub(IEnumerable<Tag>? tags = null) : EntityRepositoryStub<Tag>(tags), ITagDal;
    private sealed class TagCategoryDalStub(IEnumerable<TagCategory>? categories = null) : EntityRepositoryStub<TagCategory>(categories), ITagCategoryDal;
    private sealed class ProductVerificationDalStub(IEnumerable<ProductVerification>? items = null) : EntityRepositoryStub<ProductVerification>(items), IProductVerificationDal;
    private sealed class ProductEditProposalDalStub(IEnumerable<ProductEditProposal>? items = null) : EntityRepositoryStub<ProductEditProposal>(items), IProductEditProposalDal;
    private sealed class ProductEditProposalTagDalStub(IEnumerable<ProductEditProposalTag>? items = null) : EntityRepositoryStub<ProductEditProposalTag>(items), IProductEditProposalTagDal;
    private sealed class ProductEditProposalCategoryDalStub(IEnumerable<ProductEditProposalCategory>? items = null) : EntityRepositoryStub<ProductEditProposalCategory>(items), IProductEditProposalCategoryDal;
}
