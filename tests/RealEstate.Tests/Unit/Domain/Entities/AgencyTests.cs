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

    [Fact]
    public void SetLogo_ShouldSetLogoMetadata()
    {
        // Arrange
        var agency = CreateAgency();

        // Act
        agency.SetLogo(
            logoUrl: "/uploads/agencies/agency-id/logo/logo.png",
            storedFileName: "logo.png",
            contentType: "image/png",
            sizeBytes: 128);

        // Assert
        agency.LogoUrl.Should()
            .Be("/uploads/agencies/agency-id/logo/logo.png");

        agency.LogoStoredFileName.Should().Be("logo.png");
        agency.LogoContentType.Should().Be("image/png");
        agency.LogoSizeBytes.Should().Be(128);
    }

    [Fact]
    public void RemoveLogo_ShouldClearLogoMetadata()
    {
        // Arrange
        var agency = CreateAgency();

        agency.SetLogo(
            logoUrl: "/uploads/agencies/agency-id/logo/logo.png",
            storedFileName: "logo.png",
            contentType: "image/png",
            sizeBytes: 128);

        // Act
        agency.RemoveLogo();

        // Assert
        agency.LogoUrl.Should().BeNull();
        agency.LogoStoredFileName.Should().BeNull();
        agency.LogoContentType.Should().BeNull();
        agency.LogoSizeBytes.Should().BeNull();
    }

    [Fact]
    public void RemoveLogo_ShouldBeIdempotent()
    {
        // Arrange
        var agency = CreateAgency();

        // Act
        Action act = () =>
        {
            agency.RemoveLogo();
            agency.RemoveLogo();
        };

        // Assert
        act.Should().NotThrow();
        agency.LogoUrl.Should().BeNull();
        agency.LogoStoredFileName.Should().BeNull();
        agency.LogoContentType.Should().BeNull();
        agency.LogoSizeBytes.Should().BeNull();
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Rejected)]
    public void Approve_ShouldSetStatusToActive_WhenTransitionIsAllowed(
    AgencyStatus initialStatus)
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(initialStatus);

        // Act
        agency.Approve();

        // Assert
        agency.Status.Should().Be(AgencyStatus.Active);
    }

    [Fact]
    public void Approve_ShouldRemainActive_WhenAgencyIsAlreadyActive()
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(AgencyStatus.Active);

        // Act
        Action act = agency.Approve;

        // Assert
        act.Should().NotThrow();
        agency.Status.Should().Be(AgencyStatus.Active);
    }

    [Fact]
    public void Approve_ShouldThrow_WhenAgencyIsDisabled()
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(AgencyStatus.Disabled);

        // Act
        Action act = agency.Approve;

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Disabled agencies cannot be approved.");

        agency.Status.Should().Be(AgencyStatus.Disabled);
    }

    [Fact]
    public void Reject_ShouldSetStatusToRejected_WhenAgencyIsPendingVerification()
    {
        // Arrange
        Agency agency =
            CreateAgencyWithStatus(AgencyStatus.PendingVerification);

        // Act
        agency.Reject();

        // Assert
        agency.Status.Should().Be(AgencyStatus.Rejected);
    }

    [Fact]
    public void Reject_ShouldRemainRejected_WhenAgencyIsAlreadyRejected()
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(AgencyStatus.Rejected);

        // Act
        Action act = agency.Reject;

        // Assert
        act.Should().NotThrow();
        agency.Status.Should().Be(AgencyStatus.Rejected);
    }

    [Theory]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Disabled)]
    public void Reject_ShouldThrow_WhenTransitionIsNotAllowed(
        AgencyStatus initialStatus)
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(initialStatus);

        // Act
        Action act = agency.Reject;

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "Only pending verification agencies can be rejected.");

        agency.Status.Should().Be(initialStatus);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Rejected)]
    public void Disable_ShouldSetStatusToDisabled_WhenAgencyIsNotDisabled(
        AgencyStatus initialStatus)
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(initialStatus);

        // Act
        agency.Disable();

        // Assert
        agency.Status.Should().Be(AgencyStatus.Disabled);
    }

    [Fact]
    public void Disable_ShouldRemainDisabled_WhenAgencyIsAlreadyDisabled()
    {
        // Arrange
        Agency agency = CreateAgencyWithStatus(AgencyStatus.Disabled);

        // Act
        Action act = agency.Disable;

        // Assert
        act.Should().NotThrow();
        agency.Status.Should().Be(AgencyStatus.Disabled);
    }

    private static Agency CreateAgencyWithStatus(
    AgencyStatus status)
    {
        Agency agency = CreateAgency();

        switch (status)
        {
            case AgencyStatus.PendingVerification:
                break;

            case AgencyStatus.Active:
                agency.Approve();
                break;

            case AgencyStatus.Rejected:
                agency.Reject();
                break;

            case AgencyStatus.Disabled:
                agency.Disable();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Unsupported agency status.");
        }

        return agency;
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
