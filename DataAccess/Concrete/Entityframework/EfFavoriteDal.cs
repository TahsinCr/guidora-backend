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
    public class EfFavoriteDal : EfEntityPurgeRepositoryBase<Favorite, GuidoraDbContext>, IFavoriteDal
    {
        public EfFavoriteDal(GuidoraDbContext context) : base(context) { }

        public Task<List<Favorite>> GetByUserPageAsync(int userId, int offset, int pageSize,
            CancellationToken cancellationToken = default) => Context.Favorites.AsNoTracking()
            .Where(item => item.UserId == userId).OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id).Skip(offset).Take(pageSize).ToListAsync(cancellationToken);

        public Task<int> CountByUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Context.Favorites.CountAsync(item => item.UserId == userId, cancellationToken);

        public async Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default)
        {
            var affected = await Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Favorites" ("UserId", "ProductId", "CreatedAt")
                VALUES ({favorite.UserId}, {favorite.ProductId}, {favorite.CreatedAt})
                ON CONFLICT ("UserId", "ProductId") DO NOTHING
                """, cancellationToken);
            return affected == 1;
        }
    }
}
