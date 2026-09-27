using System;

namespace Entities.DTOs;

public sealed record CommunityVerificationReviewDto(
    int ProductId,
    DateTime ReachedAt,
    int PositiveVotes,
    int CurrentlyEligiblePositiveVotes,
    int DistinctIpSignals,
    int VotesWithoutIpSignal,
    int DistinctClientSignals,
    int VotesWithoutClientSignal);
