using Core.DataAccess;
using Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IAuditLogDal
    {
        void Add(AuditLog auditLog);
        Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
        IReadOnlyList<AuditLog> GetByEntity(string entityType, string entityId);
        Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, string entityId,
            CancellationToken cancellationToken = default);
    }
}
