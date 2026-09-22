using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductChangelogDal : EfEntityRepositoryBase<ProductChangelog, GuidoraDbContext>, IProductChangelogDal
    {
        public EfProductChangelogDal(GuidoraDbContext context) : base(context) { }
    }
}
