using Business.Abstract;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataAccess.Abstract;
using Core.DataAccess;

namespace Business.Concrete
{
    public class OperationClaimManager : IOperationClaimService
    {
        private readonly IOperationClaimDal _operationClaimDal;

        public OperationClaimManager(IOperationClaimDal operationClaimDal)
        {
            _operationClaimDal = operationClaimDal;
        }

        public IDataResult<OperationClaim> GetByName(string name)
        {
            var operationClaim = _operationClaimDal.Get(item => item.Name == name);
            return operationClaim == null
                ? new ErrorDataResult<OperationClaim>("Operation claim was not found.")
                : new SuccessDataResult<OperationClaim>(operationClaim);
        }

    }
}
