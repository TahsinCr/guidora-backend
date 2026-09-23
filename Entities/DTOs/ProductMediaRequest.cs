using Core.Entities;

namespace Entities.DTOs;

public sealed class ProductMediaRequest : IDto
{
    public string MediaType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
