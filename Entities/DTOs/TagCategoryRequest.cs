using Core.Entities;

namespace Entities.DTOs;

public sealed class TagCategoryRequest : IDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool AllowsMultipleSelection { get; set; }
}
