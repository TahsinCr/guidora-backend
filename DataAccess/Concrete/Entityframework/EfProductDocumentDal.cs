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
    public class EfProductDocumentDal : EfEntityPurgeRepositoryBase<ProductDocument, GuidoraDbContext>, IProductDocumentDal
    {
        public EfProductDocumentDal(GuidoraDbContext context) : base(context) { }

        public async Task<PagedResult<ProductDocumentListItemDto>> GetPageAsync(int productId, int page,
            int pageSize, CancellationToken cancellationToken = default)
        {
            var query = Context.ProductDocuments.AsNoTracking().Where(item => item.ProductId == productId);
            var count = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(item => item.DocumentType).ThenBy(item => item.Title)
                .ThenBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(item => new ProductDocumentListItemDto(item.Id, item.ProductId,
                    item.DocumentType, item.Title, item.CreatedAt, item.UpdatedAt))
                .ToListAsync(cancellationToken);
            return new PagedResult<ProductDocumentListItemDto>(items, page, pageSize, count);
        }
    }
}
