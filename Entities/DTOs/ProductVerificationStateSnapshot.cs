namespace Entities.DTOs;

public sealed record ProductVerificationStateSnapshot(
    bool AdminVerified, bool CommunityVerified, bool CommunityRevoked,
    bool CommunityPendingReview, bool CommunityReviewRejected, bool? CurrentUserVote);
