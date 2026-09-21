using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductReportDal : EfEntityRepositoryBase<ProductReport, NorthwindContext>, IProductReportDal
    {
        public EfProductReportDal(NorthwindContext context) : base(context) { }
    }
}
