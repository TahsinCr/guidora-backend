using System;

namespace Entities.DTOs;

public sealed record ProductDocumentListItemDto(
    int Id, int ProductId, string DocumentType, string Title,
    DateTime CreatedAt, DateTime? UpdatedAt);
