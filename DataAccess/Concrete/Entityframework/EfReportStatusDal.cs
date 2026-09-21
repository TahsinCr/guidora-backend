using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfReportStatusDal : EfEntityRepositoryBase<ReportStatus, NorthwindContext>, IReportStatusDal
    {
        public EfReportStatusDal(NorthwindContext context) : base(context) { }
    }
}
