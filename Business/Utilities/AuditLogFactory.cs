using System;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Entities;

namespace Business.Utilities;

internal static class AuditLogFactory
{
    private const int LargeContentThresholdBytes = 32 * 1024;
    private const int MaxInlineAuditDiffBytes = 8 * 1024;

    public static AuditLog Create(int? actorUserId, string entityType, string entityId, string action,
        object oldValues, object newValues)
    {
        var oldNode = JsonSerializer.SerializeToNode(oldValues);
        var newNode = JsonSerializer.SerializeToNode(newValues);
        CompactLargeContent(oldNode, newNode);
        return new AuditLog
        {
            ActorUserId = actorUserId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValues = oldNode?.ToJsonString() ?? "{}",
            NewValues = newNode?.ToJsonString() ?? "{}",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void CompactLargeContent(JsonNode? oldNode, JsonNode? newNode)
    {
        if (oldNode is not JsonObject oldObject || newNode is not JsonObject newObject)
            return;

        var oldContent = GetContent(oldObject);
        var newContent = GetContent(newObject);
        if ((oldContent is null || Encoding.UTF8.GetByteCount(oldContent) <= LargeContentThresholdBytes) &&
            (newContent is null || Encoding.UTF8.GetByteCount(newContent) <= LargeContentThresholdBytes))
            return;

        var oldMetadata = oldContent is null ? null : ContentMetadata(oldContent);
        var newMetadata = newContent is null ? null : ContentMetadata(newContent);
        if (oldContent is not null && newContent is not null && newMetadata is not null)
        {
            var prefix = 0;
            while (prefix < oldContent.Length && prefix < newContent.Length && oldContent[prefix] == newContent[prefix])
                prefix++;
            var suffix = 0;
            while (suffix < oldContent.Length - prefix && suffix < newContent.Length - prefix &&
                   oldContent[oldContent.Length - suffix - 1] == newContent[newContent.Length - suffix - 1])
                suffix++;
            var inserted = newContent.Substring(prefix, newContent.Length - prefix - suffix);
            if (Encoding.UTF8.GetByteCount(inserted) <= MaxInlineAuditDiffBytes)
            {
                newMetadata["change"] = new JsonObject
                {
                    ["startChar"] = prefix,
                    ["removedCharCount"] = oldContent.Length - prefix - suffix,
                    ["unchangedSuffixCharCount"] = suffix,
                    ["insertedText"] = inserted
                };
            }
            else
            {
                newMetadata["diffOmitted"] = true;
            }
        }

        if (oldMetadata is not null) oldObject["Content"] = oldMetadata;
        if (newMetadata is not null) newObject["Content"] = newMetadata;
    }

    private static string? GetContent(JsonObject value) =>
        value["Content"] is JsonValue contentNode && contentNode.TryGetValue<string>(out var content)
            ? content
            : null;

    private static JsonObject ContentMetadata(string content) => new()
    {
        ["sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
        ["utf8Bytes"] = Encoding.UTF8.GetByteCount(content),
        ["charCount"] = content.Length
    };
}
