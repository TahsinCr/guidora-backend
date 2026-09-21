using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductChangelogDal : EfEntityRepositoryBase<ProductChangelog, NorthwindContext>, IProductChangelogDal
    {
        public EfProductChangelogDal(NorthwindContext context) : base(context) { }
    }
}
