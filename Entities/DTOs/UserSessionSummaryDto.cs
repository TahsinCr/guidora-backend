using System;

namespace Entities.DTOs;

public sealed record UserSessionSummaryDto(
    Guid Id, string DeviceName, DateTime CreatedAt, DateTime? LastUsedAt,
    DateTime ExpiresAt, DateTime AbsoluteExpiresAt, bool IsPersistent, bool IsCurrent);
