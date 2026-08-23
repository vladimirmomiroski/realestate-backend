using FluentAssertions;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Application.Listings.Queries.SearchLocationCandidates;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class SearchLocationCandidatesHandlerTests
{
    [Fact]
    public async Task PersonalCreatorGetsOrderedPreviewsAndOneBoundTokenPerCandidate()
    {
        var context = new TestContext();
        context.Listing.Translations.Add(Translation(
            context.Listing,
            "mk",
            "Скопје",
            "Центар",
            "Улица Македонија 10",
            "Дебар Маало"));
        context.Geocoder.Result = GeocodingSearchResult.Success(
        [
            Candidate("first-ref", "First address", 41.99m, 21.42m,
                LocationPrecision.ExactAddress),
            Candidate("second-ref", "Broad area", 42.01m, 21.44m,
                LocationPrecision.Approximate)
        ]);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync(" EN ");

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().Equal(
            new ListingLocationCandidateResponse(
                "First address",
                41.99m,
                21.42m,
                LocationPrecision.ExactAddress,
                "protected-1"),
            new ListingLocationCandidateResponse(
                "Broad area",
                42.01m,
                21.44m,
                LocationPrecision.Approximate,
                "protected-2"));
        context.Geocoder.CallCount.Should().Be(1);
        context.Geocoder.LastInput!.Location.LanguageCode.Should().Be("en");
        context.TokenProtector.Claims.Should().HaveCount(2);

        string expectedFingerprint = ListingLocationFingerprint.Compute(
            CanonicalLocation(context.Listing));

        context.TokenProtector.Claims.Select(claims =>
                (
                    claims.ListingId,
                    claims.ActorUserId,
                    claims.ProviderKey,
                    claims.ProviderResultReference,
                    claims.LanguageCode,
                    claims.LocationFingerprint))
            .Should().Equal(
                (context.Listing.Id, context.Actor.Id, "provider", "first-ref",
                    "en", expectedFingerprint),
                (context.Listing.Id, context.Actor.Id, "provider", "second-ref",
                    "en", expectedFingerprint));

        typeof(ListingLocationCandidateResponse).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "Label",
                "PreviewLatitude",
                "PreviewLongitude",
                "Precision",
                "ConfirmationToken");
    }

    [Theory]
    [InlineData(AgencyMemberRole.Owner)]
    [InlineData(AgencyMemberRole.Agent)]
    public async Task ActiveOwnerOrAgentCanSearchForPendingAgencyDraft(
        AgencyMemberRole role)
    {
        var context = new TestContext();
        context.AssignAgency(
            AgencyStatus.PendingVerification,
            role,
            AgencyMemberStatus.Active);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Success);
        context.Geocoder.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task PendingVerificationCreatorCanManageDraftLocation()
    {
        var context = new TestContext(UserStatus.PendingVerification);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Success);
        context.Geocoder.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task MissingPrincipalIsUnauthorizedBeforeRepositoriesOrProvider()
    {
        var context = new TestContext();
        context.CurrentUser.UserId = null;

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.AuthenticationInvalidPrincipal);
        context.UserRepository.CallCount.Should().Be(0);
        context.ListingRepository.CallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingDatabaseUserIsUnauthorizedBeforeListingOrProvider()
    {
        var context = new TestContext();
        context.UserRepository.UserResult = null;

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.AuthenticationInvalidPrincipal);
        context.ListingRepository.CallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task DisabledUserIsForbiddenBeforeListingOrProvider()
    {
        var context = new TestContext(UserStatus.Disabled);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationAccountDisabled);
        context.ListingRepository.CallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AgencyMemberRole.Manager, AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Disabled)]
    public async Task UnauthorizedAgencyMembershipNeverCallsProvider(
        AgencyMemberRole role,
        AgencyMemberStatus status)
    {
        var context = new TestContext();
        context.AssignAgency(AgencyStatus.Active, role, status);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AgencyNonmemberIsForbiddenBeforeProvider()
    {
        var context = new TestContext();
        context.AssignAgency(
            AgencyStatus.Active,
            AgencyMemberRole.Owner,
            AgencyMemberStatus.Active);
        context.AgencyRepository.MemberAccessResult = null;

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingListingUsesExistingNotFoundAndNeverCallsProvider()
    {
        var context = new TestContext();
        context.ListingRepository.ListingResult = null;

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.ResourceNotFound);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task InaccessiblePersonalListingIsForbiddenBeforeProvider()
    {
        var context = new TestContext();
        context.Listing.AssignCreator(Guid.NewGuid());

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ActiveListingIsRejectedBeforeProvider()
    {
        var context = new TestContext();
        StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(context.Listing);
        context.Listing.Publish().IsReady.Should().BeTrue();

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictResourceState);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("city")]
    [InlineData("municipality")]
    [InlineData("address")]
    public async Task IncompleteCanonicalLocationIsRejectedBeforeProvider(
        string missingField)
    {
        var context = new TestContext();
        ListingTranslation translation = context.Listing.Translations.Single();

        if (missingField == "city")
        {
            translation.City = "  ";
        }
        else if (missingField == "municipality")
        {
            translation.Municipality = null;
        }
        else
        {
            translation.AddressLine = "\u00a0";
        }

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.ValidationError);
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        result.ValidationKey.Should().Be("location");
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RequestedTranslationIsSelectedAndCanonicalized()
    {
        var context = new TestContext();
        context.Listing.Translations.Add(Translation(
            context.Listing,
            "mk",
            " Скопје ",
            " Центар ",
            " Улица Македонија 10 ",
            null));

        await context.HandleAsync(" MK ");

        context.Geocoder.LastInput!.Location.Should().Be(
            CanonicalListingLocation.From(
            [
                new CanonicalListingLocationInput(
                    "mk",
                    " Скопје ",
                    " Центар ",
                    " Улица Македонија 10 ",
                    null)
            ]).Translations.Single());
    }

    [Fact]
    public async Task MissingRequestedTranslationFallsBackToMacedonian()
    {
        var context = new TestContext();
        context.Listing.Translations.Add(Translation(
            context.Listing,
            "mk",
            "Скопје",
            "Центар",
            "Македонија 10",
            null));

        await context.HandleAsync("fr");

        context.Geocoder.LastInput!.Location.LanguageCode.Should().Be("mk");
    }

    [Fact]
    public async Task WithoutRequestedOrMacedonianTranslationFallbackIsDeterministic()
    {
        var context = new TestContext();
        context.Listing.Translations.Clear();
        context.Listing.Translations.Add(Translation(
            context.Listing, "fr", "Paris", "Paris", "Rue 1", null));
        context.Listing.Translations.Add(Translation(
            context.Listing, "de", "Berlin", "Berlin", "Straße 1", null));

        await context.HandleAsync("it");

        context.Geocoder.LastInput!.Location.LanguageCode.Should().Be("de");
    }

    [Fact]
    public async Task EmptyProviderSuccessReturnsSuccessfulEmptyCollection()
    {
        var context = new TestContext();
        context.Geocoder.Result = GeocodingSearchResult.Success([]);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().BeEmpty();
        context.TokenProtector.Claims.Should().BeEmpty();
    }

    [Theory]
    [InlineData(GeocodingSearchOutcome.PermanentFailure,
        ServiceResultStatus.DependencyUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingSearchOutcome.Unavailable,
        ServiceResultStatus.DependencyUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingSearchOutcome.MalformedResponse,
        ServiceResultStatus.DependencyUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingSearchOutcome.RateLimited,
        ServiceResultStatus.RateLimited,
        ErrorCodes.RateLimitGeocodingExceeded)]
    public async Task TypedProviderFailureMapsToExistingApplicationFailure(
        GeocodingSearchOutcome providerOutcome,
        ServiceResultStatus expectedStatus,
        string expectedCode)
    {
        var context = new TestContext();
        context.Geocoder.Result = GeocodingSearchResult.Failure(providerOutcome);

        ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>> result =
            await context.HandleAsync("en");

        result.Status.Should().Be(expectedStatus);
        result.ErrorCode.Should().Be(expectedCode);
        result.Value.Should().BeNull();
        context.TokenProtector.Claims.Should().BeEmpty();
    }

    private static GeocodingCandidate Candidate(
        string reference,
        string label,
        decimal latitude,
        decimal longitude,
        LocationPrecision precision)
    {
        return new GeocodingCandidate(
            "provider",
            reference,
            label,
            latitude,
            longitude,
            precision);
    }

    private static ListingTranslation Translation(
        Listing listing,
        string languageCode,
        string city,
        string municipality,
        string addressLine,
        string? neighborhood)
    {
        return new ListingTranslation
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            Listing = listing,
            LanguageCode = languageCode,
            Title = $"Title {languageCode}",
            Description = $"Description {languageCode}",
            City = city,
            Municipality = municipality,
            AddressLine = addressLine,
            Neighborhood = neighborhood
        };
    }

    private static CanonicalListingLocation CanonicalLocation(Listing listing)
    {
        return CanonicalListingLocation.From(listing.Translations.Select(
            translation => new CanonicalListingLocationInput(
                translation.LanguageCode,
                translation.City,
                translation.Municipality,
                translation.AddressLine,
                translation.Neighborhood)));
    }

    private static InvalidOperationException UnexpectedCall(string memberName)
    {
        return new InvalidOperationException($"Unexpected call to {memberName}.");
    }

    private sealed class TestContext
    {
        public TestContext(UserStatus userStatus = UserStatus.Active)
        {
            Actor = new User(
                "author@example.com",
                "password-hash",
                "Test",
                "Author",
                phoneNumber: null,
                status: userStatus);
            Listing = new Listing
            {
                Id = Guid.NewGuid(),
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Apartment,
                Price = 100_000m,
                Currency = "EUR",
                AreaSquareMeters = 70m
            };
            Listing.AssignCreator(Actor.Id);
            Listing.Translations.Add(Translation(
                Listing,
                "en",
                "Skopje",
                "Centar",
                "Macedonia Street 10",
                null));

            CurrentUser = new FakeCurrentUserService { UserId = Actor.Id };
            UserRepository = new FakeUserRepository { UserResult = Actor };
            ListingRepository = new FakeListingAuthoringRepository
            {
                ListingResult = Listing
            };
            AgencyRepository = new FakeAgencyRepository();
            Geocoder = new FakeListingGeocoder
            {
                Result = GeocodingSearchResult.Success([])
            };
            TokenProtector = new FakeTokenProtector();
            Handler = new SearchLocationCandidatesHandler(
                ListingRepository,
                UserRepository,
                new AgencyListingAccessChecker(AgencyRepository),
                CurrentUser,
                Geocoder,
                TokenProtector);
        }

        public User Actor { get; }
        public Listing Listing { get; }
        public FakeCurrentUserService CurrentUser { get; }
        public FakeUserRepository UserRepository { get; }
        public FakeListingAuthoringRepository ListingRepository { get; }
        public FakeAgencyRepository AgencyRepository { get; }
        public FakeListingGeocoder Geocoder { get; }
        public FakeTokenProtector TokenProtector { get; }
        public SearchLocationCandidatesHandler Handler { get; }

        public void AssignAgency(
            AgencyStatus agencyStatus,
            AgencyMemberRole role,
            AgencyMemberStatus memberStatus)
        {
            var agency = new Agency(
                "Agency",
                "agency",
                description: null,
                phoneNumber: null,
                email: null,
                websiteUrl: null,
                addressLine: null,
                city: null,
                municipality: null);

            if (agencyStatus == AgencyStatus.Active)
            {
                agency.Approve();
            }
            else if (agencyStatus == AgencyStatus.Disabled)
            {
                agency.Disable();
            }
            else if (agencyStatus == AgencyStatus.Rejected)
            {
                agency.Reject();
            }

            Listing.AssignAgency(agency.Id);
            AgencyRepository.AgencyResult = agency;
            AgencyRepository.MemberAccessResult = new AgencyMemberAccessReadModel
            {
                Role = role,
                Status = memberStatus
            };
        }

        public Task<ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>>
            HandleAsync(string? languageCode)
        {
            return Handler.HandleAsync(
                new SearchLocationCandidatesQuery(Listing.Id, languageCode),
                CancellationToken.None);
        }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }

        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? UserResult { get; set; }

        public int CallCount { get; private set; }

        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(UserResult);
        }

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(ExistsByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByNormalizedEmailReadOnlyAsync));
        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task AddAsync(User user, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(AddAsync));
        public Task<UserRegistrationPersistenceResult> PersistRegistrationAsync(User user, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(PersistRegistrationAsync));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SaveChangesAsync));
    }

    private sealed class FakeListingAuthoringRepository
        : IListingAuthoringRepository
    {
        public Listing? ListingResult { get; set; }

        public int CallCount { get; private set; }

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(ListingResult);
        }

        public Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(BeginWriteAsync));
    }

    private sealed class FakeListingGeocoder : IListingGeocoder
    {
        public GeocodingSearchResult Result { get; set; } = default!;

        public int CallCount { get; private set; }

        public GeocodingSearchInput? LastInput { get; private set; }

        public Task<GeocodingSearchResult> SearchAsync(
            GeocodingSearchInput input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastInput = input;
            return Task.FromResult(Result);
        }

        public Task<GeocodingResolutionResult> ResolveAsync(
            GeocodingReference reference,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(ResolveAsync));
    }

    private sealed class FakeTokenProtector
        : ILocationConfirmationTokenProtector
    {
        public List<LocationConfirmationTokenClaims> Claims { get; } = [];

        public string Protect(LocationConfirmationTokenClaims claims)
        {
            Claims.Add(claims);
            return $"protected-{Claims.Count}";
        }

        public LocationConfirmationTokenUnprotectResult Unprotect(
            string protectedToken) => throw UnexpectedCall(nameof(Unprotect));
    }

    private sealed class FakeAgencyRepository : IAgencyRepository
    {
        public Agency? AgencyResult { get; set; }

        public AgencyMemberAccessReadModel? MemberAccessResult { get; set; }

        public Task<Agency?> GetByIdReadOnlyAsync(Guid agencyId, CancellationToken cancellationToken) => Task.FromResult(AgencyResult);
        public Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(Guid agencyId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(MemberAccessResult);
        public Task<AgencyCreationPersistenceResult> CreateAsync(Agency agency, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(CreateAsync));
        public Task<Agency?> GetBySlugReadOnlyAsync(string slug, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetBySlugReadOnlyAsync));
        public Task<Agency?> GetByIdForUpdateAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task<Agency?> GetByIdWithMembersForUpdateAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdWithMembersForUpdateAsync));
        public void AddMember(AgencyMember member) => throw UnexpectedCall(nameof(AddMember));
        public Task<IAgencyOwnerMutationScope?> BeginLastActiveOwnerMutationAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(BeginLastActiveOwnerMutationAsync));
        public Task<AgencyMember?> GetMemberByIdForUpdateAsync(Guid agencyId, Guid memberId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetMemberByIdForUpdateAsync));
        public Task<AgencyDashboardSummaryReadModel?> GetDashboardSummaryReadOnlyAsync(Guid agencyId, DateTime utcNow, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetDashboardSummaryReadOnlyAsync));
        public Task<int> CountActiveOwnersAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(CountActiveOwnersAsync));
        public Task<IReadOnlyList<UserAgencyMembershipReadModel>> GetByUserIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByUserIdReadOnlyAsync));
        public Task<IReadOnlyList<AgencyMemberReadModel>> GetMembersByAgencyIdReadOnlyAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetMembersByAgencyIdReadOnlyAsync));
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SlugExistsAsync));
        public Task<bool> ExistsAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(ExistsAsync));
        public Task<bool> IsActiveMemberAsync(Guid agencyId, Guid userId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(IsActiveMemberAsync));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SaveChangesAsync));
    }
}
