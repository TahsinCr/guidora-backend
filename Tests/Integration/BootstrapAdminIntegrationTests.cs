using System;
using System.Transactions;
using System.Threading.Tasks;
using Business.Concrete;
using Core.Utilities.Security.Hashing;
using DataAccess.Concrete.Entityframework;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration;

public sealed class BootstrapAdminIntegrationTests
{
    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task BootstrapCreatesAdminOnceAndDoesNotChangeExistingAccount()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await using var context = new GuidoraDbContext(options);
        var unitOfWork = new GuidoraUnitOfWork(context);
        var userService = new UserManager(new EfUserDal(context), unitOfWork);
        var sut = new BootstrapAdminManager(userService, new EfRoleDal(context), new EfUserRoleDal(context),
            new EfUserProfileDal(context), new EfUserAuthenticationStateDal(context), unitOfWork);
        var email = $"bootstrap-{Guid.NewGuid():N}@example.test";
        const string password = "integration-test-password-32-characters";

        var created = await sut.EnsureCreatedAsync("bootstrap-test", email, password);
        var user = await context.Users.SingleAsync(item => item.Email == email);
        var repeated = await sut.EnsureCreatedAsync("different-name", email,
            "different-password-that-is-at-least-32-characters");

        Assert.True(created.Success);
        Assert.True(repeated.Success);
        Assert.Equal("bootstrap-test", user.FirstName);
        Assert.True(HashingHelper.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt));
        Assert.True(await context.UserRoles.AnyAsync(item => item.UserId == user.Id && item.RoleId == 3));
        Assert.True(await context.UserProfiles.AnyAsync(item => item.UserId == user.Id &&
            item.DisplayName == "bootstrap-test"));
        Assert.True(await context.UserAuthenticationStates.AnyAsync(item => item.UserId == user.Id &&
            item.AuthorizationVersion == 1));
        Assert.Equal(1, await context.Users.CountAsync(item => item.Email == email));

        // The uncompleted ambient transaction rolls back this integration fixture.
    }
}
