using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfReportReasonDal : EfEntityRepositoryBase<ReportReason, GuidoraDbContext>, IReportReasonDal
    {
        public EfReportReasonDal(GuidoraDbContext context) : base(context) { }
    }
}
