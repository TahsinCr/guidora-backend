using Core.Entities;

namespace Entities.DTOs;

public sealed class CategoryRequest : IDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
