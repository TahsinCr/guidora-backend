using System.Collections.Generic;
using Core.Entities;

namespace Core.DataAccess;

/// <summary>
/// Explicit hard-delete capability for DALs whose domain lifecycle permits purging.
/// It is intentionally separate from the default repository contract.
/// </summary>
public interface IEntityPurgeRepository<T> where T : class, IEntity, new()
{
    void Purge(T entity);
    void PurgeRange(IEnumerable<T> entities);
}
