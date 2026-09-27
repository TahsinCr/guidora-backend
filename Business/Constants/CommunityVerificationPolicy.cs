namespace Business.Constants;

public static class CommunityVerificationPolicy
{
    public const int PositiveVoteQuorum = 30;
    public const int MinimumAccountAgeDays = 7;
    public const string LegacyPolicyRevocationNote =
        "Revoked by V1 voter eligibility and moderator review policy migration.";
}
