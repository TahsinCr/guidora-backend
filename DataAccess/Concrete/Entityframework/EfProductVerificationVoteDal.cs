using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfProductVerificationVoteDal :
    EfEntityRepositoryBase<ProductVerificationVote, GuidoraDbContext>, IProductVerificationVoteDal
{
    public EfProductVerificationVoteDal(GuidoraDbContext context) : base(context) { }
}
