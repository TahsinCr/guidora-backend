using Business.BusinessRules.Catalog;
using Business.Concrete;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
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
    public void ProductDeleteCommandChangesStatusWithoutPhysicalDeletion()
    {
        var product = new Product { ProductId = 8, ProductStatusId = 1 };
        var productDal = new ProductDalStub([product]);
        var categoryDal = new CategoryDalStub();
        var statusDal = new ProductStatusDalStub([new ProductStatus { Id = 3, Code = "deleted" }]);
        var unitOfWork = new UnitOfWorkStub();
        var manager = new ProductManager(productDal, new ProductCategoryDalStub(), statusDal, unitOfWork,
            new ProductBusinessRules(productDal, categoryDal));

        var result = manager.Delete(product.ProductId);

        Assert.True(result.Success);
        Assert.Equal(3, product.ProductStatusId);
        Assert.Equal(1, productDal.UpdateCount);
        Assert.Equal(0, productDal.DeleteCount);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class ProductDalStub(IEnumerable<Product>? products = null) : EntityRepositoryStub<Product>(products), IProductDal;
    private sealed class CategoryDalStub(IEnumerable<Category>? categories = null) : EntityRepositoryStub<Category>(categories), ICategoryDal;
    private sealed class ProductStatusDalStub(IEnumerable<ProductStatus>? statuses = null) : EntityRepositoryStub<ProductStatus>(statuses), IProductStatusDal;
    private sealed class ProductCategoryDalStub(IEnumerable<ProductCategory>? links = null) : EntityRepositoryStub<ProductCategory>(links), IProductCategoryDal;
}
