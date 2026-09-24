using Business.Abstract;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using Core.Utilities.Results;
using Business.Constants;
using Core.DataAccess;
using System.Threading;

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

        public async Task<IDataResult<List<OperationClaim>>> GetClaimsAsync(User user, CancellationToken cancellationToken = default)
        {
            return new SuccessDataResult<List<OperationClaim>>(await _userDal.GetClaimsAsync(user, cancellationToken));
        }

        public async Task<IResult> GrantOperationClaimAsync(User user, OperationClaim operationClaim, CancellationToken cancellationToken = default)
        {
            _userDal.AddClaim(user, operationClaim);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _sessionInvalidationService.InvalidateUserSessionsAsync(user.Id, cancellationToken);
        }

        public async Task<IDataResult<User>> GetByMailAsync(string email, CancellationToken cancellationToken = default)
        {
            User? result = await _userDal.GetAsync(u => u.Email == email, cancellationToken);

            if (result != null) return new SuccessDataResult<User>(result);
            else return new ErrorDataResult<User>(Messages.UserNotFound);
        }

        public async Task<IDataResult<User>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            User? result = await _userDal.GetAsync(user => user.Id == id, cancellationToken);

            if (result != null) return new SuccessDataResult<User>(result);
            return new ErrorDataResult<User>(Messages.UserNotFound);
        }

        public async Task<IResult> RegisterUserAsync(User user, CancellationToken cancellationToken = default)
        {
            await _userDal.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult();
        }

        public async Task<IResult> UpdateUserAsync(User user, CancellationToken cancellationToken = default)
        {
            _userDal.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult();
        }
    }
}
