using System;
using System.Text.Json;
using Entities;

namespace Business.Utilities;

internal static class AuditLogFactory
{
    public static AuditLog Create(int? actorUserId, string entityType, string entityId, string action,
        object oldValues, object newValues) => new()
    {
        ActorUserId = actorUserId,
        EntityType = entityType,
        EntityId = entityId,
        Action = action,
        OldValues = JsonSerializer.Serialize(oldValues),
        NewValues = JsonSerializer.Serialize(newValues),
        CreatedAt = DateTime.UtcNow
    };
}
