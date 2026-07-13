using RealEstate.Domain.Common;
using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class AgencyInvitation : IAuditableEntity
{
    private AgencyInvitation()
    {
    }

    public AgencyInvitation(
        Guid agencyId,
        string email,
        string normalizedEmail,
        string token,
        string code,
        AgencyMemberRole role,
        Guid invitedByUserId,
        DateTime expiresAtUtc)
    {
        if (agencyId == Guid.Empty)
        {
            throw new ArgumentException("Agency id cannot be empty.", nameof(agencyId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Normalized email is required.", nameof(normalizedEmail));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token is required.", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Invited by user id cannot be empty.", nameof(invitedByUserId));
        }

        Id = Guid.NewGuid();
        AgencyId = agencyId;
        Email = email.Trim();
        NormalizedEmail = normalizedEmail.Trim();
        Token = token.Trim();
        Code = code.Trim();
        Role = role;
        Status = AgencyInvitationStatus.Pending;
        InvitedByUserId = invitedByUserId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Accept(Guid acceptedByUserId, DateTime utcNow)
    {
        if (acceptedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Accepted by user id cannot be empty.", nameof(acceptedByUserId));
        }

        if (Status != AgencyInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invitations can be accepted.");
        }

        if (ExpiresAtUtc <= utcNow)
        {
            throw new InvalidOperationException("Expired invitation cannot be accepted.");
        }

        Status = AgencyInvitationStatus.Accepted;
        AcceptedByUserId = acceptedByUserId;
        AcceptedAtUtc = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status != AgencyInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invitations can be cancelled.");
        }

        Status = AgencyInvitationStatus.Cancelled;
        CancelledAtUtc = utcNow;
    }

    public void MarkExpired(DateTime utcNow)
    {
        if (Status != AgencyInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invitations can be expired.");
        }

        if (ExpiresAtUtc > utcNow)
        {
            throw new InvalidOperationException("Invitation has not expired yet.");
        }

        Status = AgencyInvitationStatus.Expired;
    }

    public Guid Id { get; private set; }

    public Guid AgencyId { get; private set; }

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public string Token { get; private set; } = null!;

    public string Code { get; private set; } = null!;

    public AgencyMemberRole Role { get; private set; }

    public AgencyInvitationStatus Status { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }

    public Agency Agency { get; private set; } = null!;

    public User InvitedByUser { get; private set; } = null!;

    public User? AcceptedByUser { get; private set; }
}