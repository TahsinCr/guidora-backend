namespace Entities.DTOs;

public sealed record CommunityVoteReviewStats(
    int ProductId,
    int PositiveVotes,
    int NegativeVotes,
    int EligiblePositiveVotesAtCast,
    int EligibleNegativeVotesAtCast,
    int DistinctIpSignals,
    int VotesWithoutIpSignal,
    int DistinctClientSignals,
    int VotesWithoutClientSignal);
