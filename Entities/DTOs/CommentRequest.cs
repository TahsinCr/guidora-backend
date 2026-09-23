using Core.Entities;

namespace Entities.DTOs;

public sealed class CommentRequest : IDto
{
    public string Content { get; set; } = string.Empty;
}
