using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete.Entityframework
{
    public class EfAuditLogDal : IAuditLogDal
    {
        private readonly GuidoraDbContext _context;

        public EfAuditLogDal(GuidoraDbContext context)
        {
            _context = context;
        }

        public void Add(AuditLog auditLog) => _context.AuditLogs.Add(auditLog);

        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
            _context.AuditLogs.AddAsync(auditLog, cancellationToken).AsTask();

        public IReadOnlyList<AuditLog> GetByEntity(string entityType, string entityId) =>
            _context.AuditLogs.AsNoTracking()
                .Where(log => log.EntityType == entityType && log.EntityId == entityId)
                .OrderByDescending(log => log.CreatedAt)
                .ToList();

        public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, string entityId,
            CancellationToken cancellationToken = default) =>
            await _context.AuditLogs.AsNoTracking()
                .Where(log => log.EntityType == entityType && log.EntityId == entityId)
                .OrderByDescending(log => log.CreatedAt)
                .ToListAsync(cancellationToken);
    }
}
