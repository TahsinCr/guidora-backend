using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfTagDal : EfEntityRepositoryBase<Tag, NorthwindContext>, ITagDal
    {
        public EfTagDal(NorthwindContext context) : base(context) { }
    }
}
