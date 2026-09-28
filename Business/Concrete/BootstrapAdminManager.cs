using System;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Business.Constants;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using DataAccess.Abstract;
using Entities;

namespace Business.Concrete;

public sealed class BootstrapAdminManager(
    IUserService userService,
    IRoleDal roleDal,
    IUserRoleDal userRoleDal,
    IUserProfileDal userProfileDal,
    IUserAuthenticationStateDal authenticationStateDal,
    IUnitOfWork unitOfWork) : IBootstrapAdminService
{
    [TransactionScopeAspect]
    public async Task<IResult> EnsureCreatedAsync(string username, string email, string password,
        CancellationToken cancellationToken = default)
    {
        username = username.Trim();
        email = email.Trim();
        if (username.Length is 0 or > 100 || email.Length is 0 or > 320 || password.Length < 32)
            return new ErrorResult("Bootstrap administrator settings are invalid.");

        await unitOfWork.AcquireTransactionLockAsync(TransactionLockKeys.BootstrapAdminProvisioning,
            cancellationToken);

        var existingUser = await userService.GetByMailAsync(email, cancellationToken);
        if (existingUser.Success)
            return new SuccessResult("Bootstrap administrator already exists; no changes were made.");

        var adminRole = await roleDal.GetAsync(role => role.Code == "admin" && role.IsActive, cancellationToken);
        if (adminRole is null)
            return new ErrorResult("The active Admin role is not configured.");

        HashingHelper.CreatePasswordHash(password, out var passwordHash, out var passwordSalt);
        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email,
            FirstName = username,
            LastName = "Admin",
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Status = true,
            CreatedAt = now
        };

        var registration = await userService.RegisterUserAsync(user, cancellationToken);
        if (!registration.Success) return registration;

        await userRoleDal.AddAsync(new UserRole { UserId = user.Id, RoleId = adminRole.Id }, cancellationToken);
        await userProfileDal.AddAsync(new UserProfile
        {
            UserId = user.Id,
            DisplayName = username,
            Name = $"{username} Admin",
            CreatedAt = now
        }, cancellationToken);
        await authenticationStateDal.AddAsync(new UserAuthenticationState
        {
            UserId = user.Id,
            AuthorizationVersion = 1,
            UpdatedAt = now
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SuccessResult("Bootstrap administrator was created.");
    }
}
