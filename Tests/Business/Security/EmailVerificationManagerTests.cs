using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Tests.Core.TestDoubles;
using System.Net.Mail;

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

    [Fact]
    public async Task DeliveryFailureExpiresUndeliveredChallengeAndReturnsServiceUnavailable()
    {
        var user = new User { Id = 9, Status = true, Email = "voter@example.test" };
        var challenges = new ChallengeDal();
        var sender = new SenderStub { Fail = true };
        var manager = new EmailVerificationManager(new UserDal([user]), challenges, sender,
            new UnitOfWorkStub());

        var result = await manager.RequestAsync(user.Id);

        Assert.IsAssignableFrom<IServiceUnavailableResult>(result);
        Assert.True(Assert.Single(challenges.GetAll()).ExpiresAt <= DateTime.UtcNow);
        Assert.False((await manager.ConfirmAsync(user.Id, sender.Token!)).Success);
        sender.Fail = false;
        Assert.True((await manager.RequestAsync(user.Id)).Success);
    }

    private sealed class UserDal(IEnumerable<User> users) : EntityRepositoryStub<User>(users), IUserDal
    {
        public Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<OperationClaim>());
    }

    private sealed class ChallengeDal : EntityRepositoryStub<EmailVerificationChallenge>, IEmailVerificationChallengeDal
    {
        public Task ExpireIfMatchingAsync(int userId, string tokenHash, DateTime expiredAt,
            CancellationToken cancellationToken = default)
        {
            var challenge = Get(item => item.UserId == userId && item.TokenHash == tokenHash);
            if (challenge is not null) challenge.ExpiresAt = expiredAt;
            return Task.CompletedTask;
        }
    }

    private sealed class SenderStub : IEmailVerificationSender
    {
        public bool Fail { get; set; }
        public string? Address { get; private set; }
        public string? Token { get; private set; }
        public Task SendAsync(string address, string token, CancellationToken cancellationToken = default)
        {
            Address = address;
            Token = token;
            if (Fail) throw new SmtpException("Delivery failed.");
            return Task.CompletedTask;
        }
    }
}
