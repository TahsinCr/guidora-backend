namespace Entities.DTOs;

public sealed record ProductVerificationSummaryDto(
    bool AdminVerified,
    bool CommunityVerified,
    bool CommunityRevoked,
    int PositiveVotes,
    int RequiredPositiveVotes,
    bool? CurrentUserHasVoted,
    bool? CurrentUserVote);
