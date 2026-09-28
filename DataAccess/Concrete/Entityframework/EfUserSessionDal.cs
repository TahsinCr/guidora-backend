using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Entities.DTOs;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserSessionDal : EfEntityRepositoryBase<UserSession, GuidoraDbContext>, IUserSessionDal
    {
        public EfUserSessionDal(GuidoraDbContext context) : base(context) { }

        public async Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            var updatedRows = await Context.UserSessions
                .Where(session => session.Id == sessionId && session.RevokedAt == null &&
                                  session.ExpiresAt > revokedAt && session.AbsoluteExpiresAt > revokedAt)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(session => session.RevokedAt, revokedAt)
                    .SetProperty(session => session.LastUsedAt, revokedAt)
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
                                      session.ReplayDetectedAt == null && session.AbsoluteExpiresAt > detectedAt)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.ReplayDetectedAt, detectedAt),
                    cancellationToken);
            return updatedRows == 1;
        }

        public async Task<PagedResult<UserSessionSummaryDto>> GetActivePageAsync(int userId, Guid currentSessionId,
            int page, int pageSize, DateTime now, CancellationToken cancellationToken = default)
        {
            var query = Context.UserSessions.AsNoTracking().Where(session =>
                session.UserId == userId && session.RevokedAt == null &&
                session.ExpiresAt > now && session.AbsoluteExpiresAt > now);
            var count = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(session => session.CreatedAt).ThenBy(session => session.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(session => new UserSessionSummaryDto(session.Id, session.DeviceName,
                    session.CreatedAt, session.LastUsedAt, session.ExpiresAt,
                    session.AbsoluteExpiresAt, session.IsPersistent, session.Id == currentSessionId))
                .ToListAsync(cancellationToken);
            return new PagedResult<UserSessionSummaryDto>(items, page, pageSize, count);
        }

        public async Task<IReadOnlyList<Guid>> RevokeOwnedFamilyAsync(int userId, Guid sessionId, DateTime now,
            CancellationToken cancellationToken = default)
        {
            var familyId = await Context.UserSessions.AsNoTracking()
                .Where(session => session.Id == sessionId && session.UserId == userId)
                .Select(session => (Guid?)session.SessionFamilyId).SingleOrDefaultAsync(cancellationToken);
            if (!familyId.HasValue) return [];

            var query = Context.UserSessions.Where(session => session.UserId == userId &&
                session.SessionFamilyId == familyId.Value && session.RevokedAt == null);
            var ids = await query.Select(session => session.Id).ToListAsync(cancellationToken);
            if (ids.Count > 0)
                await query.Where(session => ids.Contains(session.Id))
                    .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now),
                        cancellationToken);
            return ids;
        }

        public async Task<IReadOnlyList<Guid>> RevokeOtherActiveAsync(int userId, Guid currentSessionId,
            DateTime now, CancellationToken cancellationToken = default)
        {
            var query = Context.UserSessions.Where(session => session.UserId == userId &&
                session.Id != currentSessionId && session.RevokedAt == null &&
                session.ExpiresAt > now && session.AbsoluteExpiresAt > now);
            var ids = await query.Select(session => session.Id).ToListAsync(cancellationToken);
            if (ids.Count > 0)
                await query.Where(session => ids.Contains(session.Id))
                    .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now),
                        cancellationToken);
            return ids;
        }
    }
}
