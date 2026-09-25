using Core.DataAccess;
using Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IUserSessionDal : IEntityRepository<UserSession>
    {
        Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default);
        Task<bool> TryRevokeAsync(Guid sessionId, DateTime revokedAt, CancellationToken cancellationToken = default);
        Task<bool> TryMarkReplayDetectedAsync(Guid sessionId, DateTime detectedAt,
            CancellationToken cancellationToken = default);
    }
}
