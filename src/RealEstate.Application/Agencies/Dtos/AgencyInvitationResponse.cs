using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Dtos;

public sealed class AgencyInvitationResponse
{
    public Guid Id { get; init; }

    public Guid AgencyId { get; init; }

    public string Email { get; init; } = null!;

    public AgencyMemberRole Role { get; init; }

    public AgencyInvitationStatus Status { get; init; }

    public string Token { get; init; } = null!;

    public string Code { get; init; } = null!;

    public Guid InvitedByUserId { get; init; }

    public Guid? AcceptedByUserId { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? AcceptedAtUtc { get; init; }

    public DateTime? CancelledAtUtc { get; init; }
}