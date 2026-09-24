using Core.Entities;

namespace Entities.DTOs;

public sealed class TagRequest : IDto
{
    public int TagCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsExclusive { get; set; }
}
