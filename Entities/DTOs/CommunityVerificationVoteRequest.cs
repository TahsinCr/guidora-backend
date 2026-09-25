using Core.Entities;

namespace Entities.DTOs;

public sealed class CommunityVerificationVoteRequest : IDto
{
    public bool Approved { get; set; }
}
