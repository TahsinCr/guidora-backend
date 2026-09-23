using Business.BusinessRules.Catalog;
using Business.Concrete;
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
    public void RejectsDuplicateNameButAllowsCurrentProductDuringUpdate()
    {
        var product = new Product { ProductId = 8, Name = "Guidora" };
        var rules = new ProductBusinessRules(new ProductDalStub([product]), new CategoryDalStub());

        Assert.False(rules.EnsureNameIsAvailable("Guidora").Success);
        Assert.True(rules.EnsureNameIsAvailable("Guidora", product.ProductId).Success);
    }

    [Fact]
    public void RequiresAtLeastOneExistingActiveCategory()
    {
        var categories = new CategoryDalStub([
            new Category { CategoryId = 1, IsActive = true },
            new Category { CategoryId = 2, IsActive = false }]);
        var rules = new ProductBusinessRules(new ProductDalStub(), categories);

        Assert.False(rules.EnsureCategoriesAreActive([]).Success);
        Assert.True(rules.EnsureCategoriesAreActive([1, 1]).Success);
        Assert.False(rules.EnsureCategoriesAreActive([1, 2]).Success);
        Assert.False(rules.EnsureCategoriesAreActive([1, 99]).Success);
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
            new ProductBusinessRules(productDal, categories), new TagBusinessRules());

        var result = await manager.CreateAsync(new CreateProductRequest
        {
            Name = "Guidora", Slug = "guidora", CategoryIds = [1], TagIds = [2]
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
            new ProductBusinessRules(productDal, categoryDal), new TagBusinessRules());

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
