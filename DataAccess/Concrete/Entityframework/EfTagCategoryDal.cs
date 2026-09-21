using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfTagCategoryDal : EfEntityRepositoryBase<TagCategory, NorthwindContext>, ITagCategoryDal
    {
        public EfTagCategoryDal(NorthwindContext context) : base(context) { }
    }
}
