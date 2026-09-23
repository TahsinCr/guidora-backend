using Core.Entities;

namespace Entities.DTOs;

public sealed class ProductDocumentRequest : IDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
