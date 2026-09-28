using System;
using Core.Entities;

namespace Entities
{
    public class UserSession : IEntity
    {
        public Guid Id { get; set; }
        public Guid SessionFamilyId { get; set; }
        public int UserId { get; set; }
        public string RefreshTokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime AbsoluteExpiresAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime? ReplayDetectedAt { get; set; }
        public Guid? ReplacedBySessionId { get; set; }
        public bool IsPersistent { get; set; }
        public string DeviceName { get; set; } = string.Empty;
    }
}
