using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfReportReasonDal : EfEntityRepositoryBase<ReportReason, NorthwindContext>, IReportReasonDal
    {
        public EfReportReasonDal(NorthwindContext context) : base(context) { }
    }
}
