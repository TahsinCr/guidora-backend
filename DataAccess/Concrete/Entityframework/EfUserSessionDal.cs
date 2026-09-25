using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserSessionDal : EfEntityRepositoryBase<UserSession, GuidoraDbContext>, IUserSessionDal
    {
        public EfUserSessionDal(GuidoraDbContext context) : base(context) { }

        public async Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            var updatedRows = await Context.UserSessions
                .Where(session => session.Id == sessionId && session.RevokedAt == null && session.ExpiresAt > revokedAt)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(session => session.RevokedAt, revokedAt)
                    .SetProperty(session => session.ReplacedBySessionId, replacementSessionId), cancellationToken);

            return updatedRows == 1;
        }

        public async Task<bool> TryRevokeAsync(Guid sessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            var updatedRows = await Context.UserSessions
                .Where(session => session.Id == sessionId && session.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, revokedAt),
                    cancellationToken);

            return updatedRows == 1;
        }

        public async Task<bool> TryMarkReplayDetectedAsync(Guid sessionId, DateTime detectedAt,
            CancellationToken cancellationToken = default)
        {
            var updatedRows = await Context.UserSessions
                .Where(session => session.Id == sessionId && session.ReplacedBySessionId != null &&
                                  session.ReplayDetectedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.ReplayDetectedAt, detectedAt),
                    cancellationToken);
            return updatedRows == 1;
        }
    }
}
