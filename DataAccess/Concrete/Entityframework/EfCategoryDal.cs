using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfCategoryDal : EfEntityRepositoryBase<Category, GuidoraDbContext>, ICategoryDal
    {
        public EfCategoryDal(GuidoraDbContext context) : base(context) { }
    }
}
