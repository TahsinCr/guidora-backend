using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfCommentStatusDal : EfEntityRepositoryBase<CommentStatus, GuidoraDbContext>, ICommentStatusDal
    {
        public EfCommentStatusDal(GuidoraDbContext context) : base(context) { }
    }
}
