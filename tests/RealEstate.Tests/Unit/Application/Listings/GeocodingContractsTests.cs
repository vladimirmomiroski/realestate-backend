using FluentAssertions;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class GeocodingContractsTests
{
    [Fact]
    public void SearchSuccess_SnapshotsCandidateOrderAndDoesNotExposeMutability()
    {
        var source = new List<GeocodingCandidate>
        {
            Candidate("first"),
            Candidate("second")
        };

        GeocodingSearchResult result = GeocodingSearchResult.Success(source);
        source.Reverse();
        source.Add(Candidate("third"));

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(GeocodingSearchOutcome.Success);
        result.Candidates.Select(candidate => candidate.ResultReference)
            .Should().Equal("first", "second");
        result.Candidates.Should().BeAssignableTo<IReadOnlyList<GeocodingCandidate>>();
    }

    [Theory]
    [InlineData(GeocodingSearchOutcome.PermanentFailure)]
    [InlineData(GeocodingSearchOutcome.RateLimited)]
    [InlineData(GeocodingSearchOutcome.Unavailable)]
    [InlineData(GeocodingSearchOutcome.MalformedResponse)]
    public void SearchFailure_ProvidesAClosedOutcomeWithoutCandidateData(
        GeocodingSearchOutcome outcome)
    {
        GeocodingSearchResult result = GeocodingSearchResult.Failure(outcome);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(outcome);
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void SearchFailure_RejectsSuccessAndUnknownOutcomes()
    {
        Action success = () => GeocodingSearchResult.Failure(
            GeocodingSearchOutcome.Success);
        Action unknown = () => GeocodingSearchResult.Failure(
            (GeocodingSearchOutcome)int.MaxValue);

        success.Should().Throw<ArgumentOutOfRangeException>();
        unknown.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ResolutionSuccess_CarriesOnlyProviderNeutralCanonicalSnapshot()
    {
        var snapshot = new ResolvedGeocodingSnapshot(
            "provider",
            "reference",
            41.9981m,
            21.4254m,
            LocationPrecision.ExactAddress,
            "Display name");

        GeocodingResolutionResult result =
            GeocodingResolutionResult.Success(snapshot);

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(GeocodingResolutionOutcome.Success);
        result.Snapshot.Should().BeSameAs(snapshot);
    }

    [Theory]
    [InlineData(GeocodingResolutionOutcome.NotFound)]
    [InlineData(GeocodingResolutionOutcome.Stale)]
    [InlineData(GeocodingResolutionOutcome.PermanentFailure)]
    [InlineData(GeocodingResolutionOutcome.RateLimited)]
    [InlineData(GeocodingResolutionOutcome.Unavailable)]
    [InlineData(GeocodingResolutionOutcome.MalformedResponse)]
    public void ResolutionFailure_ProvidesTypedOutcomeWithoutTrustedSnapshot(
        GeocodingResolutionOutcome outcome)
    {
        GeocodingResolutionResult result =
            GeocodingResolutionResult.Failure(outcome);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(outcome);
        result.Snapshot.Should().BeNull();
    }

    [Fact]
    public void GeocoderOperations_RequireExplicitCallerCancellationToken()
    {
        Type[] searchParameters = typeof(IListingGeocoder)
            .GetMethod(nameof(IListingGeocoder.SearchAsync))!
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Type[] resolveParameters = typeof(IListingGeocoder)
            .GetMethod(nameof(IListingGeocoder.ResolveAsync))!
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        searchParameters.Should().Equal(
            typeof(GeocodingSearchInput),
            typeof(CancellationToken));
        resolveParameters.Should().Equal(
            typeof(GeocodingReference),
            typeof(CancellationToken));
    }

    [Fact]
    public void SearchInput_UsesAnEntryFromTheSharedCanonicalRepresentation()
    {
        CanonicalListingLocation location = CanonicalListingLocation.From(
        [
            new CanonicalListingLocationInput(
                " EN ",
                " Skopje ",
                " Centar ",
                " Address ",
                null)
        ]);

        var input = new GeocodingSearchInput(location.Translations.Single());

        input.Location.LanguageCode.Should().Be("en");
        input.Location.City.Should().Be("Skopje");
        input.Location.Municipality.Should().Be("Centar");
        input.Location.AddressLine.Should().Be("Address");
        input.Location.Neighborhood.Should().BeNull();
    }

    private static GeocodingCandidate Candidate(string reference)
    {
        return new GeocodingCandidate(
            "provider",
            reference,
            reference,
            41.9981m,
            21.4254m,
            LocationPrecision.City);
    }
}
