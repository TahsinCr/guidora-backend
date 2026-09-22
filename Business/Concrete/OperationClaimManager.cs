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
        private readonly IUnitOfWork _unitOfWork;

        public OperationClaimManager(IOperationClaimDal operationClaimDal, IUnitOfWork unitOfWork)
        {
            _operationClaimDal = operationClaimDal;
            _unitOfWork = unitOfWork;
        }

        public IDataResult<List<OperationClaim>> GetAll()
        {
            return new SuccessDataResult<List<OperationClaim>>(_operationClaimDal.GetAll());
        }

        public IDataResult<OperationClaim> GetByName(string name)
        {
            var operationClaim = _operationClaimDal.Get(item => item.Name == name);
            return operationClaim == null
                ? new ErrorDataResult<OperationClaim>("Operation claim was not found.")
                : new SuccessDataResult<OperationClaim>(operationClaim);
        }

        public IResult Add(OperationClaim operationClaim)
        {
            _operationClaimDal.Add(operationClaim);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        public IResult Delete(OperationClaim operationClaim)
        {
            _operationClaimDal.Delete(operationClaim);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        public IResult Update(OperationClaim operationClaim)
        {
            _operationClaimDal.Update(operationClaim);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }
    }
}
