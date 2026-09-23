using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DataAccess.Concrete.Entityframework
{
    public class EfCommentDal : EfEntityRepositoryBase<Comment, GuidoraDbContext>, ICommentDal
    {
        public EfCommentDal(GuidoraDbContext context) : base(context) { }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<Comment>> GetVisibleByProductPageAsync(
            int productId, int offset, int pageSize, System.Threading.CancellationToken cancellationToken = default) =>
            GetVisibleQuery(productId).OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
                .Skip(offset).Take(pageSize).ToListAsync(cancellationToken);

        public System.Threading.Tasks.Task<int> CountVisibleByProductAsync(int productId,
            System.Threading.CancellationToken cancellationToken = default) =>
            GetVisibleQuery(productId).CountAsync(cancellationToken);

        private IQueryable<Comment> GetVisibleQuery(int productId) =>
            from comment in Context.Comments.AsNoTracking()
             join commentStatus in Context.CommentStatuses on comment.CommentStatusId equals commentStatus.Id
             join user in Context.Users on comment.UserId equals user.Id
             join product in Context.Products on comment.ProductId equals product.ProductId
             join productStatus in Context.ProductStatuses on product.ProductStatusId equals productStatus.Id
             where comment.ProductId == productId
                   && commentStatus.Code == "active"
                   && commentStatus.IsVisible
                   && user.Status
                   && productStatus.Code == "published"
             select comment;
    }
}
