using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductEditProposalDal : EfEntityRepositoryBase<ProductEditProposal, NorthwindContext>, IProductEditProposalDal
    {
        public EfProductEditProposalDal(NorthwindContext context) : base(context) { }
    }
}
