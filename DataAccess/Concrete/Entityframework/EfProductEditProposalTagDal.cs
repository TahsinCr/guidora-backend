using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductEditProposalTagDal : EfEntityRepositoryBase<ProductEditProposalTag, GuidoraDbContext>, IProductEditProposalTagDal
    {
        public EfProductEditProposalTagDal(GuidoraDbContext context) : base(context) { }
    }
}
