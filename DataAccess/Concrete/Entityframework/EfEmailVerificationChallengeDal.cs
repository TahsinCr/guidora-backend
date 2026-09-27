using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfEmailVerificationChallengeDal
    : EfEntityRepositoryBase<EmailVerificationChallenge, GuidoraDbContext>, IEmailVerificationChallengeDal
{
    private readonly GuidoraDbContext _context;

    public EfEmailVerificationChallengeDal(GuidoraDbContext context) : base(context) => _context = context;

    public async Task ExpireIfMatchingAsync(int userId, string tokenHash, DateTime expiredAt,
        CancellationToken cancellationToken = default)
    {
        await _context.EmailVerificationChallenges
            .Where(item => item.UserId == userId && item.TokenHash == tokenHash && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ExpiresAt, expiredAt), cancellationToken);
    }
}
