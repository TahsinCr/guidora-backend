using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfOperationClaimDal : EfEntityRepositoryBase<OperationClaim, NorthwindContext>, IOperationClaimDal
    {
        public EfOperationClaimDal(NorthwindContext context) : base(context) { }
    }
}
