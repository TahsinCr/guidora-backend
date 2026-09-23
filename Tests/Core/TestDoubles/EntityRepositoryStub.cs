using Core.DataAccess;
using Core.Entities;
using System.Linq.Expressions;

namespace Tests.Core.TestDoubles;

public abstract class EntityRepositoryStub<TEntity> : IEntityRepository<TEntity>
    where TEntity : class, IEntity, new()
{
    protected EntityRepositoryStub(IEnumerable<TEntity>? entities = null) =>
        Entities = entities?.ToList() ?? new List<TEntity>();

    protected List<TEntity> Entities { get; }
    public int UpdateCount { get; private set; }
    public int DeleteCount { get; private set; }

    public TEntity? Get(Expression<Func<TEntity, bool>> filter) => Entities.AsQueryable().SingleOrDefault(filter);
    public List<TEntity> GetAll(Expression<Func<TEntity, bool>>? filter = null) =>
        filter is null ? Entities.ToList() : Entities.AsQueryable().Where(filter).ToList();
    public Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(Get(filter));
    public Task<List<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetAll(filter));
    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entities.AsQueryable().Any(filter));
    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(filter is null ? Entities.Count : Entities.AsQueryable().Count(filter));
    public void Add(TEntity entity) => Entities.Add(entity);
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Add(entity);
        return Task.CompletedTask;
    }
    public void AddRange(IEnumerable<TEntity> entities) => Entities.AddRange(entities);
    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        AddRange(entities);
        return Task.CompletedTask;
    }
    public void Update(TEntity entity) => UpdateCount++;
    public void Delete(TEntity entity) => DeleteCount++;
    public void DeleteRange(IEnumerable<TEntity> entities) => DeleteCount += entities.Count();
}

public sealed class UnitOfWorkStub : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public int SaveChanges()
    {
        SaveCount++;
        return 1;
    }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}
