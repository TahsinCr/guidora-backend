using System;
using Core.Entities;

namespace Entities;

public sealed class ProductVerificationVote : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public bool Approved { get; set; }
    public bool EligibleAtCast { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? OriginIpHash { get; set; }
    public string? OriginClientHash { get; set; }
}
