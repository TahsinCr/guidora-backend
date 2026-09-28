using Core.DataAccess;
using Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Entities.DTOs;

namespace DataAccess.Abstract
{
    public interface IUserSessionDal : IEntityRepository<UserSession>
    {
        Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default);
        Task<bool> TryRevokeAsync(Guid sessionId, DateTime revokedAt, CancellationToken cancellationToken = default);
        Task<bool> TryMarkReplayDetectedAsync(Guid sessionId, DateTime detectedAt,
            CancellationToken cancellationToken = default);
        Task<PagedResult<UserSessionSummaryDto>> GetActivePageAsync(int userId, Guid currentSessionId,
            int page, int pageSize, DateTime now, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Guid>> RevokeOwnedFamilyAsync(int userId, Guid sessionId, DateTime now,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Guid>> RevokeOtherActiveAsync(int userId, Guid currentSessionId, DateTime now,
            CancellationToken cancellationToken = default);
    }
}
