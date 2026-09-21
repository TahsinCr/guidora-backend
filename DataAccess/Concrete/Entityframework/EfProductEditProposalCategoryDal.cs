using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductEditProposalCategoryDal : EfEntityRepositoryBase<ProductEditProposalCategory, NorthwindContext>, IProductEditProposalCategoryDal
    {
        public EfProductEditProposalCategoryDal(NorthwindContext context) : base(context) { }
    }
}
