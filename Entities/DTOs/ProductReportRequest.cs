using Core.Entities;

namespace Entities.DTOs;

public sealed class ProductReportRequest : IDto
{
    public int ReasonId { get; set; }
    public string Description { get; set; } = string.Empty;
}
