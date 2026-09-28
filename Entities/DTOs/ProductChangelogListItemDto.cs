using System;

namespace Entities.DTOs;

public sealed record ProductChangelogListItemDto(
    int Id, int ProductId, string Version, string Title,
    DateTime ReleaseDate, DateTime CreatedAt);
