namespace Entities.DTOs;

public sealed record ProductVerificationSummaryDto(
    bool AdminVerified,
    bool CommunityVerified,
    bool CommunityRevoked,
    bool CommunityPendingReview,
    bool CommunityReviewRejected,
    int PositiveVotes,
    int RequiredPositiveVotes,
    bool? CurrentUserHasVoted,
    bool? CurrentUserVote);
