using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfAuditLogDal : EfEntityRepositoryBase<AuditLog, GuidoraDbContext>, IAuditLogDal
    {
        public EfAuditLogDal(GuidoraDbContext context) : base(context) { }
    }
}
