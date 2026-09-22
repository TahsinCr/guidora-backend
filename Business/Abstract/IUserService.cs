using Core.Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Business;
using Core.DataAccess;
using Core.Utilities.Results;

namespace Business.Abstract
{
    public interface IUserService: IEntityManagerRepository<User>
    {
        IDataResult<List<OperationClaim>> GetClaims(User user);
        IResult AddClaim(User user, OperationClaim operationClaim);
        IDataResult<User> GetById(int id);
        IDataResult<User> GetByMail(string email);
    }
}
