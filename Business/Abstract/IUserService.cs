using Core.Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Utilities.Results;

namespace Business.Abstract
{
    public interface IUserService
    {
        IDataResult<List<OperationClaim>> GetClaims(User user);
        IResult GrantOperationClaim(User user, OperationClaim operationClaim);
        IDataResult<User> GetById(int id);
        IDataResult<User> GetByMail(string email);
        IResult RegisterUser(User user);
        IResult UpdateUser(User user);
    }
}
