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

    [Fact]
    public void UpdateProfile_ShouldUpdateAgencyProfile()
    {
        var agency = CreateAgency();

        agency.UpdateProfile(
            name: "Updated Agency",
            description: "Updated description",
            phoneNumber: "+38970222222",
            email: "updated@test.com",
            websiteUrl: "https://updated.test",
            addressLine: "Updated Street 1",
            city: "Skopje",
            municipality: "Karpos");

        agency.Name.Should().Be("Updated Agency");
        agency.Description.Should().Be("Updated description");
        agency.PhoneNumber.Should().Be("+38970222222");
        agency.Email.Should().Be("updated@test.com");
        agency.WebsiteUrl.Should().Be("https://updated.test");
        agency.AddressLine.Should().Be("Updated Street 1");
        agency.City.Should().Be("Skopje");
        agency.Municipality.Should().Be("Karpos");
    }

    [Fact]
    public void UpdateProfile_ShouldConvertWhitespaceOptionalFieldsToNull()
    {
        var agency = CreateAgency();

        agency.UpdateProfile(
            name: "Updated Agency",
            description: " ",
            phoneNumber: " ",
            email: " ",
            websiteUrl: " ",
            addressLine: " ",
            city: " ",
            municipality: " ");

        agency.Name.Should().Be("Updated Agency");
        agency.Description.Should().BeNull();
        agency.PhoneNumber.Should().BeNull();
        agency.Email.Should().BeNull();
        agency.WebsiteUrl.Should().BeNull();
        agency.AddressLine.Should().BeNull();
        agency.City.Should().BeNull();
        agency.Municipality.Should().BeNull();
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
