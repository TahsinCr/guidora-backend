using System;
using Core.Entities;

namespace Entities;

public sealed class CommunityVerificationReview : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public DateTime ReachedAt { get; set; }
    public string Status { get; set; } = "pending";
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Note { get; set; } = string.Empty;
}
