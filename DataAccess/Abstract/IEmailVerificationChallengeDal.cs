using Core.DataAccess;
using Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract;

public interface IEmailVerificationChallengeDal : IEntityRepository<EmailVerificationChallenge>
{
    Task ExpireIfMatchingAsync(int userId, string tokenHash, DateTime expiredAt,
        CancellationToken cancellationToken = default);
}
