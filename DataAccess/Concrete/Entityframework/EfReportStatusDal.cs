using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfReportStatusDal : EfEntityRepositoryBase<ReportStatus, GuidoraDbContext>, IReportStatusDal
    {
        public EfReportStatusDal(GuidoraDbContext context) : base(context) { }
    }
}
