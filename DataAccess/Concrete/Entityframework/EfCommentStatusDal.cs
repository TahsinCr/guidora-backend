using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfCommentStatusDal : EfEntityRepositoryBase<CommentStatus, NorthwindContext>, ICommentStatusDal
    {
        public EfCommentStatusDal(NorthwindContext context) : base(context) { }
    }
}
