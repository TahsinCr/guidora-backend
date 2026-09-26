using System;

namespace Entities.DTOs;

public sealed record ProductResponseDto(
    int ProductId,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    string WebsiteUrl,
    string LogoUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
