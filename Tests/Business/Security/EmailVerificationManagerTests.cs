using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class EmailVerificationManagerTests
{
    [Fact]
    public async Task RequestSendsOneTimeCodeAndConfirmationMarksEmailVerified()
    {
        var user = new User { Id = 9, Status = true, Email = "voter@example.test" };
        var challenges = new ChallengeDal();
        var sender = new SenderStub();
        var manager = new EmailVerificationManager(new UserDal([user]), challenges, sender,
            new UnitOfWorkStub());

        Assert.True((await manager.RequestAsync(user.Id)).Success);
        var challenge = Assert.Single(challenges.GetAll());
        Assert.Equal(64, challenge.TokenHash.Length);
        Assert.NotEqual(sender.Token, challenge.TokenHash);
        Assert.Equal("voter@example.test", sender.Address);
        Assert.True((await manager.ConfirmAsync(user.Id, sender.Token!)).Success);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotNull(challenge.UsedAt);
        Assert.IsAssignableFrom<IConflictResult>(await manager.ConfirmAsync(user.Id, sender.Token!));
    }

    [Fact]
    public async Task ExpiredOrIncorrectCodeCannotVerifyEmail()
    {
        var user = new User { Id = 9, Status = true, Email = "voter@example.test" };
        var challenges = new ChallengeDal();
        var sender = new SenderStub();
        var manager = new EmailVerificationManager(new UserDal([user]), challenges, sender,
            new UnitOfWorkStub());

        await manager.RequestAsync(user.Id);
        Assert.False((await manager.ConfirmAsync(user.Id, new string('0', 64))).Success);
        Assert.Null(user.EmailVerifiedAt);
        challenges.GetAll().Single().ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        Assert.False((await manager.ConfirmAsync(user.Id, sender.Token!)).Success);
        Assert.Null(user.EmailVerifiedAt);
    }

    private sealed class UserDal(IEnumerable<User> users) : EntityRepositoryStub<User>(users), IUserDal
    {
        public Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<OperationClaim>());
        public void AddClaim(User user, OperationClaim operationClaim) { }
    }

    private sealed class ChallengeDal : EntityRepositoryStub<EmailVerificationChallenge>, IEmailVerificationChallengeDal;

    private sealed class SenderStub : IEmailVerificationSender
    {
        public string? Address { get; private set; }
        public string? Token { get; private set; }
        public Task SendAsync(string address, string token, CancellationToken cancellationToken = default)
        {
            Address = address;
            Token = token;
            return Task.CompletedTask;
        }
    }
}
