using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspects.Transaction;
using Core.Utilities.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class UserSecurityManager : IUserSecurityService
    {
        private readonly IUserService _userService;
        private readonly IUserSessionInvalidationService _sessionInvalidationService;

        public UserSecurityManager(IUserService userService, IUserSessionInvalidationService sessionInvalidationService)
        {
            _userService = userService;
            _sessionInvalidationService = sessionInvalidationService;
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
