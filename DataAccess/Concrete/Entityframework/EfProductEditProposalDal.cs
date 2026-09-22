using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductEditProposalDal : EfEntityRepositoryBase<ProductEditProposal, GuidoraDbContext>, IProductEditProposalDal
    {
        public EfProductEditProposalDal(GuidoraDbContext context) : base(context) { }
    }
}
