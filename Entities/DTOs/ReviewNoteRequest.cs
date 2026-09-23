using Core.Entities;

namespace Entities.DTOs;

public sealed class ReviewNoteRequest : IDto
{
    public string Note { get; set; } = string.Empty;
}
