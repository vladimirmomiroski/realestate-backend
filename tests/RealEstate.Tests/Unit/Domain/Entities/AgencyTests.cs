using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class AgencyTests
{
    [Fact]
    public void AddMember_ShouldAddMember_WhenUserIsNotAlreadyMember()
    {
        // Arrange
        var agency = CreateAgency();
        var userId = Guid.NewGuid();

        // Act
        var member = agency.AddMember(userId, AgencyMemberRole.Owner);

        // Assert
        member.UserId.Should().Be(userId);
        member.AgencyId.Should().Be(agency.Id);
        member.Role.Should().Be(AgencyMemberRole.Owner);
        member.Status.Should().Be(AgencyMemberStatus.Active);
        agency.Members.Should().ContainSingle();
    }

    [Fact]
    public void AddMember_ShouldThrowInvalidOperationException_WhenUserIsAlreadyMember()
    {
        // Arrange
        var agency = CreateAgency();
        var userId = Guid.NewGuid();

        agency.AddMember(userId, AgencyMemberRole.Owner);

        // Act
        var act = () => agency.AddMember(userId, AgencyMemberRole.Agent);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("User is already a member of this agency.");
    }

    [Fact]
    public void AddMember_ShouldUseProvidedStatus_WhenStatusIsProvided()
    {
        // Arrange
        var agency = CreateAgency();
        var userId = Guid.NewGuid();

        // Act
        var member = agency.AddMember(
            userId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Pending);

        // Assert
        member.Status.Should().Be(AgencyMemberStatus.Pending);
    }

    private static Agency CreateAgency()
    {
        return new Agency(
            name: "Dom Real Estate",
            slug: "dom-real-estate",
            description: "Real estate agency in Skopje.",
            phoneNumber: "+38970123456",
            email: "agency@test.com",
            websiteUrl: "https://agency.test",
            addressLine: "Partizanska 1",
            city: "Skopje",
            municipality: "Centar");
    }
}
