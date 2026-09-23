using Core.Entities;

namespace Entities.DTOs;

public sealed class CommentVisibilityRequest : IDto
{
    public bool IsVisible { get; set; }
}
