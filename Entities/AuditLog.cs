using System;
using Core.Entities;

namespace Entities
{
    public class AuditLog : IEntity
    {
        public long Id { get; set; }
        public int? ActorUserId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string OldValues { get; set; } = "{}";
        public string NewValues { get; set; } = "{}";
        public long? RevertsAuditLogId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
