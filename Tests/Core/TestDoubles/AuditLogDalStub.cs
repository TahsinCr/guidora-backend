using DataAccess.Abstract;
using Entities;

namespace Tests.Core.TestDoubles;

public sealed class AuditLogDalStub : IAuditLogDal
{
    public List<AuditLog> Entries { get; } = [];
    public void Add(AuditLog auditLog) => Entries.Add(auditLog);
    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        Add(auditLog);
        return Task.CompletedTask;
    }
    public IReadOnlyList<AuditLog> GetByEntity(string entityType, string entityId) =>
        Entries.Where(entry => entry.EntityType == entityType && entry.EntityId == entityId).ToArray();
    public Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, string entityId,
        CancellationToken cancellationToken = default) => Task.FromResult(GetByEntity(entityType, entityId));
    public Task<AuditLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entries.SingleOrDefault(entry => entry.Id == id));
    public Task<bool> HasRevertAsync(long auditLogId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entries.Any(entry => entry.RevertsAuditLogId == auditLogId));
}
