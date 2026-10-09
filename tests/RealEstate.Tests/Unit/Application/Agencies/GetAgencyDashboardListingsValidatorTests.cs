using FluentAssertions;
using RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Agencies;

public sealed class GetAgencyDashboardListingsValidatorTests
{
    public static TheoryData<ListingStatus> DefinedStatuses =>
        new(Enum.GetValues<ListingStatus>());

    [Fact]
    public void Validate_StatusIsAbsent_ReturnsNoError()
    {
        var validator = new GetAgencyDashboardListingsValidator();

        string? error = validator.Validate(
            new GetAgencyDashboardListingsQuery());

        error.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(DefinedStatuses))]
    public void Validate_StatusIsDefined_ReturnsNoError(
        ListingStatus status)
    {
        var validator = new GetAgencyDashboardListingsValidator();

        string? error = validator.Validate(
            new GetAgencyDashboardListingsQuery { Status = status });

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_StatusIsUndefined_ReturnsBoundedError()
    {
        var validator = new GetAgencyDashboardListingsValidator();

        string? error = validator.Validate(
            new GetAgencyDashboardListingsQuery
            {
                Status = (ListingStatus)999
            });

        error.Should().Be(
            GetAgencyDashboardListingsValidator.UndefinedStatusError);
    }
}
