using Core.Entities;
using System;

namespace Entities.DTOs;

public sealed class ProductChangelogRequest : IDto
{
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
}
