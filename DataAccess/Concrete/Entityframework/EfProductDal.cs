using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
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

        public Task<List<Product>> GetPublishedPageAsync(int statusId, int? categoryId, int skip, int take,
            CancellationToken cancellationToken = default) => Context.Products.AsNoTracking()
            .Where(product => product.ProductStatusId == statusId &&
                              (!categoryId.HasValue || Context.ProductCategories.Any(link =>
                                  link.ProductId == product.ProductId && link.CategoryId == categoryId.Value)))
            .OrderBy(product => product.Name).ThenBy(product => product.ProductId)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);

        public Task<int> CountPublishedAsync(int statusId, int? categoryId,
            CancellationToken cancellationToken = default) => Context.Products.AsNoTracking()
            .Where(product => product.ProductStatusId == statusId &&
                              (!categoryId.HasValue || Context.ProductCategories.Any(link =>
                                  link.ProductId == product.ProductId && link.CategoryId == categoryId.Value)))
            .CountAsync(cancellationToken);
    }
}
