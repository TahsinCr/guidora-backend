using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Entities.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductChangelogDal : EfEntityRepositoryBase<ProductChangelog, GuidoraDbContext>, IProductChangelogDal
    {
        public EfProductChangelogDal(GuidoraDbContext context) : base(context) { }

        public async Task<PagedResult<ProductChangelogListItemDto>> GetPageAsync(int productId, int page,
            int pageSize, CancellationToken cancellationToken = default)
        {
            var query = Context.ProductChangelogs.AsNoTracking().Where(item => item.ProductId == productId);
            var count = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(item => item.ReleaseDate).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(item => new ProductChangelogListItemDto(item.Id, item.ProductId,
                    item.Version, item.Title, item.ReleaseDate, item.CreatedAt))
                .ToListAsync(cancellationToken);
            return new PagedResult<ProductChangelogListItemDto>(items, page, pageSize, count);
        }
    }
}
