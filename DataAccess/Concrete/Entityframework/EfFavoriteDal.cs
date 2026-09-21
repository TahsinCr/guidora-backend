using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfFavoriteDal : EfEntityRepositoryBase<Favorite, NorthwindContext>, IFavoriteDal
    {
        public EfFavoriteDal(NorthwindContext context) : base(context) { }
    }
}
