using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Core.Aspects.Transaction;
using Core.Utilities.Results;

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

        [TransactionScopeAspect]
        [SecuredOperationAspect("User.Manage")]
        public IResult Ban(int userId)
        {
            var userResult = _userService.GetById(userId);
            if (!userResult.Success)
            {
                return new ErrorResult(userResult.Message);
            }

            userResult.Data.Status = false;
            var updateResult = _userService.UpdateUser(userResult.Data);
            if (!updateResult.Success)
            {
                return updateResult;
            }

            return _sessionInvalidationService.InvalidateUserSessions(userId);
        }

        [TransactionScopeAspect]
        [SecuredOperationAspect("User.Manage")]
        public IResult Restore(int userId)
        {
            var userResult = _userService.GetById(userId);
            if (!userResult.Success)
            {
                return new ErrorResult(userResult.Message);
            }

            userResult.Data.Status = true;
            var updateResult = _userService.UpdateUser(userResult.Data);
            if (!updateResult.Success)
            {
                return updateResult;
            }

            return _sessionInvalidationService.InvalidateUserSessions(userId);
        }

        [TransactionScopeAspect]
        [SecuredOperationAspect("User.Manage")]
        public IResult InvalidateAuthorization(int userId)
        {
            return _sessionInvalidationService.InvalidateUserSessions(userId);
        }
    }
}
