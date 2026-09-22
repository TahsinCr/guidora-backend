using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.DataAccess.EntityFramework
{
    public class EfEntityRepositoryBase<TEntity, TContext> : IEntityRepository<TEntity>
    where TEntity : class, IEntity, new()
    where TContext : DbContext
    {
        protected TContext Context { get; }

        protected EfEntityRepositoryBase(TContext context)
        {
            Context = context;
        }

        public void Add(TEntity entity)
        {
            Context.Entry(entity).State = EntityState.Added;
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
            Context.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();

        public void AddRange(IEnumerable<TEntity> entities) => Context.Set<TEntity>().AddRange(entities);

        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
            Context.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

        public void Delete(TEntity entity)
        {
            Context.Entry(entity).State = EntityState.Deleted;
        }

        public void DeleteRange(IEnumerable<TEntity> entities) => Context.Set<TEntity>().RemoveRange(entities);

        public TEntity? Get(Expression<Func<TEntity, bool>> filter)
        {
            return Context.Set<TEntity>().SingleOrDefault(filter);
        }

        public Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default) =>
            Context.Set<TEntity>().SingleOrDefaultAsync(filter, cancellationToken);

        public List<TEntity> GetAll(Expression<Func<TEntity, bool>>? filter = null)
        {
            return filter == null
                ? Context.Set<TEntity>().ToList()
                : Context.Set<TEntity>().Where(filter).ToList();
        }

        public Task<List<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null,
            CancellationToken cancellationToken = default) =>
            filter == null
                ? Context.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken)
                : Context.Set<TEntity>().AsNoTracking().Where(filter).ToListAsync(cancellationToken);

        public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter,
            CancellationToken cancellationToken = default) =>
            Context.Set<TEntity>().AsNoTracking().AnyAsync(filter, cancellationToken);

        public Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null,
            CancellationToken cancellationToken = default) =>
            filter == null
                ? Context.Set<TEntity>().AsNoTracking().CountAsync(cancellationToken)
                : Context.Set<TEntity>().AsNoTracking().CountAsync(filter, cancellationToken);

        public void Update(TEntity entity)
        {
            Context.Entry(entity).State = EntityState.Modified;
        }
    }
}
