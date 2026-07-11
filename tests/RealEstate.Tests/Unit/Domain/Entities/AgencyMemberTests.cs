using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class AgencyMemberTests
{
    [Theory]
    [InlineData(AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberStatus.Pending)]
    public void Disable_ShouldSetStatusToDisabled_WhenMemberIsNotDisabled(
        AgencyMemberStatus initialStatus)
    {
        // Arrange
        var member = new AgencyMember(
            agencyId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            role: AgencyMemberRole.Agent,
            status: initialStatus);

        // Act
        member.Disable();

        // Assert
        member.Status.Should().Be(AgencyMemberStatus.Disabled);
    }

    [Fact]
    public void Disable_ShouldRemainDisabled_WhenMemberIsAlreadyDisabled()
    {
        // Arrange
        var member = new AgencyMember(
            agencyId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            role: AgencyMemberRole.Agent,
            status: AgencyMemberStatus.Disabled);

        // Act
        Action act = member.Disable;

        // Assert
        act.Should().NotThrow();
        member.Status.Should().Be(AgencyMemberStatus.Disabled);
    }

    [Fact]
    public void Disable_ShouldBeIdempotent()
    {
        // Arrange
        var member = new AgencyMember(
            agencyId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            role: AgencyMemberRole.Agent);

        // Act
        member.Disable();
        member.Disable();

        // Assert
        member.Status.Should().Be(AgencyMemberStatus.Disabled);
    }
}
