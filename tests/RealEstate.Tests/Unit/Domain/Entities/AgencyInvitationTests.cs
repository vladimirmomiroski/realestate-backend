using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class AgencyInvitationTests
{
    [Fact]
    public void Accept_ShouldAcceptPendingInvitation()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var acceptedByUserId = Guid.NewGuid();
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(7));

        // Act
        invitation.Accept(acceptedByUserId, utcNow);

        // Assert
        invitation.Status.Should().Be(AgencyInvitationStatus.Accepted);
        invitation.AcceptedByUserId.Should().Be(acceptedByUserId);
        invitation.AcceptedAtUtc.Should().Be(utcNow);
    }

    [Fact]
    public void Accept_ShouldThrowInvalidOperationException_WhenInvitationIsCancelled()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(7));

        invitation.Cancel(utcNow);

        // Act
        var act = () => invitation.Accept(Guid.NewGuid(), utcNow);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Only pending invitations can be accepted.");
    }

    [Fact]
    public void Accept_ShouldThrowInvalidOperationException_WhenInvitationIsExpiredByDate()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(-1));

        // Act
        var act = () => invitation.Accept(Guid.NewGuid(), utcNow);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Expired invitation cannot be accepted.");
    }

    [Fact]
    public void Cancel_ShouldCancelPendingInvitation()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(7));

        // Act
        invitation.Cancel(utcNow);

        // Assert
        invitation.Status.Should().Be(AgencyInvitationStatus.Cancelled);
        invitation.CancelledAtUtc.Should().Be(utcNow);
    }

    [Fact]
    public void Cancel_ShouldThrowInvalidOperationException_WhenInvitationIsAccepted()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(7));

        invitation.Accept(Guid.NewGuid(), utcNow);

        // Act
        var act = () => invitation.Cancel(utcNow);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Only pending invitations can be cancelled.");
    }

    [Fact]
    public void MarkExpired_ShouldExpirePendingInvitation()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(-1));

        // Act
        invitation.MarkExpired(utcNow);

        // Assert
        invitation.Status.Should().Be(AgencyInvitationStatus.Expired);
    }

    [Fact]
    public void MarkExpired_ShouldThrowInvalidOperationException_WhenInvitationHasNotExpiredYet()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var invitation = CreateInvitation(expiresAtUtc: utcNow.AddDays(7));

        // Act
        var act = () => invitation.MarkExpired(utcNow);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Invitation has not expired yet.");
    }

    private static AgencyInvitation CreateInvitation(DateTime expiresAtUtc)
    {
        return new AgencyInvitation(
            agencyId: Guid.NewGuid(),
            email: "agent@test.com",
            normalizedEmail: "AGENT@TEST.COM",
            token: Guid.NewGuid().ToString("N"),
            code: "123456",
            role: AgencyMemberRole.Agent,
            invitedByUserId: Guid.NewGuid(),
            expiresAtUtc: expiresAtUtc);
    }
}
