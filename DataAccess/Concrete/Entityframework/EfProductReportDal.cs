using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductReportDal : EfEntityRepositoryBase<ProductReport, GuidoraDbContext>, IProductReportDal
    {
        public EfProductReportDal(GuidoraDbContext context) : base(context) { }
    }
}
