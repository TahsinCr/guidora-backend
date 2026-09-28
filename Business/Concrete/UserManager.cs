using Business.Abstract;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Business.Constants;
using Core.DataAccess;
using System.Threading;

namespace Business.Concrete
{
    public class UserManager : IUserService
    {
        private readonly IUserDal _userDal;
        private readonly IUnitOfWork _unitOfWork;

        public UserManager(IUserDal userDal,
            IUnitOfWork unitOfWork)
        {
            _userDal = userDal;
            _unitOfWork = unitOfWork;
        }

        public async Task<IDataResult<List<OperationClaim>>> GetClaimsAsync(User user, CancellationToken cancellationToken = default)
        {
            return new SuccessDataResult<List<OperationClaim>>(await _userDal.GetClaimsAsync(user, cancellationToken));
        }

        public async Task<IDataResult<User>> GetByMailAsync(string email, CancellationToken cancellationToken = default)
        {
            var result = await _userDal.GetAsync(u => u.Email == email, cancellationToken);
            if (result != null) return new SuccessDataResult<User>(result);
            return new ErrorDataResult<User>(Messages.UserNotFound);
        }

        public async Task<IDataResult<User>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var result = await _userDal.GetAsync(user => user.Id == id, cancellationToken);

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
