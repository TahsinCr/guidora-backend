using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductContributorDal : EfEntityPurgeRepositoryBase<ProductContributor, GuidoraDbContext>, IProductContributorDal
    {
        public EfProductContributorDal(GuidoraDbContext context) : base(context) { }
    }
}
