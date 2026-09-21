using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfAuditLogDal : EfEntityRepositoryBase<AuditLog, NorthwindContext>, IAuditLogDal
    {
        public EfAuditLogDal(NorthwindContext context) : base(context) { }
    }
}
