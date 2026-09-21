using Core.DataAccess;
using Entities;

namespace DataAccess.Abstract
{
    public interface IAuditLogDal : IEntityRepository<AuditLog>
    {
    }
}
