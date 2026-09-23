using Core.Entities;

namespace Entities.DTOs;

public sealed class ProductContributorRequest : IDto
{
    public int UserId { get; set; }
    public string ContributionType { get; set; } = string.Empty;
}
