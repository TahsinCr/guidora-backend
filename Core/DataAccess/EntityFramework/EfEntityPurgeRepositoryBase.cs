using System.Collections.Generic;
using Core.DataAccess;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.DataAccess.EntityFramework;

/// <summary>
/// Opt-in EF repository base for entity types that permit explicit physical deletion.
/// </summary>
public abstract class EfEntityPurgeRepositoryBase<TEntity, TContext> : EfEntityRepositoryBase<TEntity, TContext>,
    IEntityPurgeRepository<TEntity>
    where TEntity : class, IEntity, new()
    where TContext : DbContext
{
    protected EfEntityPurgeRepositoryBase(TContext context) : base(context) { }

    public void Purge(TEntity entity) => Context.Entry(entity).State = EntityState.Deleted;

    public void PurgeRange(IEnumerable<TEntity> entities) => Context.Set<TEntity>().RemoveRange(entities);
}
