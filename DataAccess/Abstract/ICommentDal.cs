using Core.DataAccess;
using Entities;

namespace DataAccess.Abstract
{
    public interface ICommentDal : IEntityRepository<Comment>
    {
        System.Threading.Tasks.Task<System.Collections.Generic.List<Comment>> GetVisibleByProductPageAsync(
            int productId, int offset, int pageSize, System.Threading.CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<int> CountVisibleByProductAsync(int productId,
            System.Threading.CancellationToken cancellationToken = default);
    }
}
