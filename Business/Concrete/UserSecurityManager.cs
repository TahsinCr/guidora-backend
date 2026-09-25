using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspects.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Core.DataAccess;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Business.Concrete
{
    public class UserSecurityManager : IUserSecurityService
    {
        private readonly IUserService _userService;
        private readonly IUserSessionInvalidationService _sessionInvalidationService;
        private readonly IRoleDal _roleDal;
        private readonly IUserDal _userDal;
        private readonly IUserRoleDal _userRoleDal;
        private readonly IUnitOfWork _unitOfWork;

        public UserSecurityManager(IUserService userService, IUserSessionInvalidationService sessionInvalidationService,
            IRoleDal roleDal, IUserDal userDal, IUserRoleDal userRoleDal, IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _sessionInvalidationService = sessionInvalidationService;
            _roleDal = roleDal;
            _userDal = userDal;
            _userRoleDal = userRoleDal;
            _unitOfWork = unitOfWork;
        }

        public UserSecurityManager(IUserService userService, IUserSessionInvalidationService sessionInvalidationService)
        {
            _userService = userService;
            _sessionInvalidationService = sessionInvalidationService;
            _roleDal = null!;
            _userDal = null!;
            _userRoleDal = null!;
            _unitOfWork = null!;
        }

        [SecuredOperationAspect("User.Manage")]
        [TransactionScopeAspect]
        public async Task<IResult> BanAsync(int userId, CancellationToken cancellationToken = default)
        {
            var userResult = await _userService.GetByIdAsync(userId, cancellationToken);
            if (!userResult.Success)
            {
                return new NotFoundResult(userResult.Message);
            }

            var adminRole = await _roleDal.GetAsync(role => role.Code == "admin", cancellationToken);
            if (adminRole is not null)
            {
                await _unitOfWork.AcquireTransactionLockAsync(7319842101L, cancellationToken);
                var adminUserIds = (await _userRoleDal.GetAllAsync(item => item.RoleId == adminRole.Id, cancellationToken))
                    .Select(item => item.UserId).Distinct().ToArray();
                var activeAdmins = await _userDal.CountAsync(user => user.Status && adminUserIds.Contains(user.Id), cancellationToken);
                if (activeAdmins <= 1 && adminUserIds.Contains(userId) && userResult.Data.Status)
                    return new ConflictResult("The last active Admin account cannot be banned.");
            }

            userResult.Data.Status = false;
            var updateResult = await _userService.UpdateUserAsync(userResult.Data, cancellationToken);
            if (!updateResult.Success)
            {
                return updateResult;
            }

            return await _sessionInvalidationService.InvalidateUserSessionsAsync(userId, cancellationToken);
        }

        [SecuredOperationAspect("User.Manage")]
        [TransactionScopeAspect]
        public async Task<IResult> RestoreAsync(int userId, CancellationToken cancellationToken = default)
        {
            var userResult = await _userService.GetByIdAsync(userId, cancellationToken);
            if (!userResult.Success)
            {
                return new NotFoundResult(userResult.Message);
            }

            userResult.Data.Status = true;
            var updateResult = await _userService.UpdateUserAsync(userResult.Data, cancellationToken);
            if (!updateResult.Success)
            {
                return updateResult;
            }

            return await _sessionInvalidationService.InvalidateUserSessionsAsync(userId, cancellationToken);
        }

        [SecuredOperationAspect("User.Manage")]
        [TransactionScopeAspect]
        public Task<IResult> InvalidateAuthorizationAsync(int userId, CancellationToken cancellationToken = default)
        {
            return _sessionInvalidationService.InvalidateUserSessionsAsync(userId, cancellationToken);
        }
    }
}
