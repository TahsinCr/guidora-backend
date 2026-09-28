using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Entities.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductDal : EfEntityRepositoryBase<Product, GuidoraDbContext>, IProductDal
    {
        public EfProductDal(GuidoraDbContext context) : base(context) { }

        public Task<List<ProductListItemDto>> GetPublishedPageAsync(int statusId, int? categoryId, int skip, int take,
            CancellationToken cancellationToken = default) => Context.Products.AsNoTracking()
            .Where(product => product.ProductStatusId == statusId &&
                              (!categoryId.HasValue || Context.ProductCategories.Any(link =>
                                  link.ProductId == product.ProductId && link.CategoryId == categoryId.Value)))
            .OrderBy(product => product.Name).ThenBy(product => product.ProductId)
            .Skip(skip).Take(take)
            .Select(product => new ProductListItemDto(product.ProductId, product.Name, product.Slug,
                product.ShortDescription, product.WebsiteUrl, product.LogoUrl,
                product.CreatedAt, product.UpdatedAt))
            .ToListAsync(cancellationToken);

        public Task<int> CountPublishedAsync(int statusId, int? categoryId,
            CancellationToken cancellationToken = default) => Context.Products.AsNoTracking()
            .Where(product => product.ProductStatusId == statusId &&
                              (!categoryId.HasValue || Context.ProductCategories.Any(link =>
                                  link.ProductId == product.ProductId && link.CategoryId == categoryId.Value)))
            .CountAsync(cancellationToken);
    }
}
