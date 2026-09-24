using Core.DataAccess;
using Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IFavoriteDal : IEntityRepository<Favorite>, IEntityPurgeRepository<Favorite>
    {
        Task<List<Favorite>> GetByUserPageAsync(int userId, int offset, int pageSize,
            CancellationToken cancellationToken = default);
        Task<int> CountByUserAsync(int userId, CancellationToken cancellationToken = default);
    }
}
