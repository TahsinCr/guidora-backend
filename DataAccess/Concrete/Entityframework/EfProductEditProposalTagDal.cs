using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductEditProposalTagDal : EfEntityRepositoryBase<ProductEditProposalTag, NorthwindContext>, IProductEditProposalTagDal
    {
        public EfProductEditProposalTagDal(NorthwindContext context) : base(context) { }
    }
}
