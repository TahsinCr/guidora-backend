using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfTagCategoryDal : EfEntityRepositoryBase<TagCategory, GuidoraDbContext>, ITagCategoryDal
    {
        public EfTagCategoryDal(GuidoraDbContext context) : base(context) { }
    }
}
