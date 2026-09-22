using Business.Abstract;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Business;
using Core.DataAccess.EntityFramework;
using Core.Utilities.Results;
using Business.Constants;
using Core.DataAccess;

namespace Business.Concrete
{
    public class UserManager : IUserService
    {
        private readonly IUserDal _userDal;
        private readonly IUserSessionInvalidationService _sessionInvalidationService;
        private readonly IUnitOfWork _unitOfWork;

        public UserManager(IUserDal userDal, IUserSessionInvalidationService sessionInvalidationService,
            IUnitOfWork unitOfWork)
        {
            _userDal = userDal;
            _sessionInvalidationService = sessionInvalidationService;
            _unitOfWork = unitOfWork;
        }

        public IDataResult<List<User>> GetAll()
        {
            return new SuccessDataResult<List<User>>(_userDal.GetAll());
        }

        public IDataResult<List<OperationClaim>> GetClaims(User user)
        {
            return new SuccessDataResult<List<OperationClaim>>(_userDal.GetClaims(user));
        }

        public IResult AddClaim(User user, OperationClaim operationClaim)
        {
            _userDal.AddClaim(user, operationClaim);
            _unitOfWork.SaveChanges();
            return _sessionInvalidationService.InvalidateUserSessions(user.Id);
        }

        public IDataResult<User> GetByMail(string email)
        {
            User? result = _userDal.Get(u => u.Email == email);

            if (result != null) return new SuccessDataResult<User>(result);
            else return new ErrorDataResult<User>(Messages.UserNotFound);
        }

        public IDataResult<User> GetById(int id)
        {
            User? result = _userDal.Get(user => user.Id == id);

            if (result != null) return new SuccessDataResult<User>(result);
            return new ErrorDataResult<User>(Messages.UserNotFound);
        }

        public IResult Add(User user)
        {
            _userDal.Add(user);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        public IResult Update(User user)
        {
            _userDal.Update(user);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        public IResult Delete(User user)
        {
            return new ErrorResult("Physical user deletion is not supported.");
        }
    }
}
