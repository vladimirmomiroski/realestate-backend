using FluentAssertions;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.ConfirmListingLocation;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class ConfirmListingLocationHandlerTests
{
    private static readonly DateTimeOffset ConfirmedAt =
        new(2026, 8, 20, 10, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(LocationConfirmationTokenUnprotectOutcome.Invalid)]
    [InlineData(LocationConfirmationTokenUnprotectOutcome.Expired)]
    [InlineData(LocationConfirmationTokenUnprotectOutcome.UnsupportedVersion)]
    public async Task UnusableTokenFailsBeforeProviderAndWriteScope(
        LocationConfirmationTokenUnprotectOutcome outcome)
    {
        var context = new TestContext();
        context.TokenProtector.Result =
            LocationConfirmationTokenUnprotectResult.Failure(outcome);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.ValidationError);
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        result.ValidationKey.Should().Be("confirmationToken");
        context.Geocoder.CallCount.Should().Be(0);
        context.ListingRepository.BeginCallCount.Should().Be(0);
    }

    [Fact]
    public async Task TokenForDifferentListingFailsBeforeProviderAndWriteScope()
    {
        var context = new TestContext();
        context.SetToken(listingId: Guid.NewGuid());

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.ValidationError);
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        context.Geocoder.CallCount.Should().Be(0);
        context.ListingRepository.BeginCallCount.Should().Be(0);
    }

    [Fact]
    public async Task TokenForDifferentActorIsForbiddenBeforeProviderAndWriteScope()
    {
        var context = new TestContext();
        context.SetToken(actorUserId: Guid.NewGuid());

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        context.Geocoder.CallCount.Should().Be(0);
        context.ListingRepository.BeginCallCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingPrincipalFailsBeforeTokenProviderAndRepositories()
    {
        var context = new TestContext();
        context.CurrentUser.UserId = null;

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.AuthenticationInvalidPrincipal);
        context.UserRepository.CallCount.Should().Be(0);
        context.ListingRepository.ReadCallCount.Should().Be(0);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingDatabaseUserFailsBeforeListingTokenAndProvider()
    {
        var context = new TestContext();
        context.UserRepository.SetResults((User?)null);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        context.ListingRepository.ReadCallCount.Should().Be(0);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task DisabledUserFailsBeforeListingTokenAndProvider()
    {
        var context = new TestContext(UserStatus.Disabled);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationAccountDisabled);
        context.ListingRepository.ReadCallCount.Should().Be(0);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task PersonalNonownerFailsBeforeTokenAndProvider()
    {
        var context = new TestContext();
        context.ReadListing.AssignCreator(Guid.NewGuid());

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AgencyMemberRole.Manager, AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Disabled)]
    public async Task UnauthorizedAgencyMembershipFailsBeforeTokenAndProvider(
        AgencyMemberRole role,
        AgencyMemberStatus status)
    {
        var context = new TestContext();
        context.AssignAgency(role, status);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AgencyNonmemberFailsBeforeTokenAndProvider()
    {
        var context = new TestContext();
        context.AssignAgency(
            AgencyMemberRole.Owner,
            AgencyMemberStatus.Active);
        context.AgencyRepository.SetMemberResults(
            (AgencyMemberAccessReadModel?)null);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task NonDraftFailsBeforeTokenAndProvider()
    {
        var context = new TestContext();
        StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(context.ReadListing);
        context.ReadListing.Publish().IsReady.Should().BeTrue();

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictResourceState);
        context.TokenProtector.UnprotectCallCount.Should().Be(0);
        context.Geocoder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task FingerprintChangedSinceIssuanceFailsBeforeProvider()
    {
        var context = new TestContext();
        context.ReadListing.Translations.Single().AddressLine = "Changed 99";

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictResourceSetChanged);
        context.Geocoder.CallCount.Should().Be(0);
        context.ListingRepository.BeginCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExactTokenReferenceIsResolvedBeforeWriteScope()
    {
        var context = new TestContext();

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        context.Geocoder.Reference.Should().Be(
            new GeocodingReference("provider", "opaque-reference"));
        context.Calls.IndexOf("provider.resolve").Should().BeLessThan(
            context.Calls.IndexOf("listing.write.begin"));
        context.Geocoder.CallCount.Should().Be(1);
        context.ListingRepository.BeginCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(GeocodingResolutionOutcome.RateLimited,
        ServiceResultStatus.RateLimited,
        ErrorCodes.RateLimitGeocodingExceeded)]
    [InlineData(GeocodingResolutionOutcome.Unavailable,
        ServiceResultStatus.DependencyUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingResolutionOutcome.PermanentFailure,
        ServiceResultStatus.DependencyUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingResolutionOutcome.MalformedResponse,
        ServiceResultStatus.DependencyUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingResolutionOutcome.NotFound,
        ServiceResultStatus.Conflict,
        ErrorCodes.ConflictResourceSetChanged)]
    [InlineData(GeocodingResolutionOutcome.Stale,
        ServiceResultStatus.Conflict,
        ErrorCodes.ConflictResourceSetChanged)]
    public async Task ProviderFailureNeverAcquiresWriteScope(
        GeocodingResolutionOutcome providerOutcome,
        ServiceResultStatus expectedStatus,
        string expectedCode)
    {
        var context = new TestContext();
        context.Geocoder.Result =
            GeocodingResolutionResult.Failure(providerOutcome);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(expectedStatus);
        result.ErrorCode.Should().Be(expectedCode);
        context.ListingRepository.BeginCallCount.Should().Be(0);
        context.WriteScope.SaveCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.LockedListing.Latitude.Should().BeNull();
    }

    [Fact]
    public async Task LockedFingerprintChangeRejectsWithoutMutationOrCommit()
    {
        var context = new TestContext();
        context.LockedListing.Translations.Single().Neighborhood = "Changed";

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        AssertLockedRejection(context, result, ErrorCodes.ConflictResourceSetChanged);
    }

    [Fact]
    public async Task UserDisabledDuringProviderGapRejectsUnderLock()
    {
        var context = new TestContext();
        var disabled = new User(
            "disabled@example.com",
            "hash",
            "Disabled",
            "User",
            null,
            status: UserStatus.Disabled);
        context.UserRepository.SetResults(context.Actor, disabled);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        AssertLockedRejection(context, result, ErrorCodes.AuthorizationAccountDisabled);
    }

    [Fact]
    public async Task ListingBecomesNonDraftDuringProviderGapRejectsUnderLock()
    {
        var context = new TestContext();
        StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(context.LockedListing);
        context.LockedListing.Publish().IsReady.Should().BeTrue();
        context.LockedListing.ClearLocation();

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        AssertLockedRejection(context, result, ErrorCodes.ConflictResourceState);
    }

    [Fact]
    public async Task PersonalOwnershipChangeDuringProviderGapRejectsUnderLock()
    {
        var context = new TestContext();
        context.LockedListing.AssignCreator(Guid.NewGuid());

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        AssertLockedRejection(context, result, ErrorCodes.AuthorizationForbidden);
    }

    [Fact]
    public async Task AgencyAccessRevokedDuringProviderGapRejectsUnderLock()
    {
        var context = new TestContext();
        context.AssignAgency(
            AgencyMemberRole.Owner,
            AgencyMemberStatus.Active);
        context.AgencyRepository.SetMemberResults(
            new AgencyMemberAccessReadModel
            {
                Role = AgencyMemberRole.Owner,
                Status = AgencyMemberStatus.Active
            },
            new AgencyMemberAccessReadModel
            {
                Role = AgencyMemberRole.Owner,
                Status = AgencyMemberStatus.Pending
            });

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        AssertLockedRejection(context, result, ErrorCodes.AuthorizationForbidden);
    }

    [Fact]
    public async Task ListingDeletedDuringProviderGapReturnsNotFoundWithoutMutation()
    {
        var context = new TestContext();
        context.ListingRepository.ReturnMissingWriteScope = true;

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.ResourceNotFound);
        context.Geocoder.CallCount.Should().Be(1);
        context.ListingRepository.BeginCallCount.Should().Be(1);
        context.WriteScope.SaveCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.LockedListing.Latitude.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedResolvedProvenanceRejectsUnderLock(bool changeProvider)
    {
        var context = new TestContext();
        context.Geocoder.Result = GeocodingResolutionResult.Success(
            new ResolvedGeocodingSnapshot(
                changeProvider ? "other-provider" : "provider",
                changeProvider ? "opaque-reference" : "other-reference",
                41.99m,
                21.42m,
                LocationPrecision.City,
                "Resolved"));

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        AssertLockedRejection(context, result, ErrorCodes.ConflictResourceSetChanged);
    }

    [Theory]
    [InlineData(AgencyMemberRole.Owner)]
    [InlineData(AgencyMemberRole.Agent)]
    public async Task PendingVerificationUserCanConfirmForNonActiveAgency(
        AgencyMemberRole role)
    {
        var context = new TestContext(UserStatus.PendingVerification);
        context.AssignAgency(
            role,
            AgencyMemberStatus.Active,
            AgencyStatus.PendingVerification);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        context.AgencyRepository.MemberCallCount.Should().Be(2);
    }

    [Fact]
    public async Task SuccessPersistsOnlyExactResolvedSnapshotAndReturnsPrivateState()
    {
        var context = new TestContext();
        context.LockedListing.Price = 123_456m;
        string originalAddress =
            context.LockedListing.Translations.Single().AddressLine!;
        var snapshot = new ResolvedGeocodingSnapshot(
            "provider",
            "opaque-reference",
            41.9981m,
            21.4254m,
            LocationPrecision.Neighborhood,
            "Resolved display");
        context.Geocoder.Result = GeocodingResolutionResult.Success(snapshot);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().Be(new ListingLocationStateResponse(
            snapshot.Latitude,
            snapshot.Longitude,
            snapshot.Precision,
            snapshot.DisplayName,
            ConfirmedAt.UtcDateTime));
        context.LockedListing.Latitude.Should().Be(snapshot.Latitude);
        context.LockedListing.Longitude.Should().Be(snapshot.Longitude);
        context.LockedListing.LocationPrecision.Should().Be(snapshot.Precision);
        context.LockedListing.GeocodingProviderKey.Should().Be(snapshot.ProviderKey);
        context.LockedListing.GeocodingResultReference.Should().Be(snapshot.ResultReference);
        context.LockedListing.GeocodedDisplayName.Should().Be(snapshot.DisplayName);
        context.LockedListing.LocationConfirmedAtUtc.Should().Be(ConfirmedAt.UtcDateTime);
        context.LockedListing.Price.Should().Be(123_456m);
        context.LockedListing.Status.Should().Be(ListingStatus.Draft);
        context.LockedListing.Translations.Single().AddressLine.Should().Be(originalAddress);
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(1);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.Calls.IndexOf("listing.save").Should().BeLessThan(
            context.Calls.IndexOf("listing.commit"));
        context.UserRepository.CallCount.Should().Be(2);

        typeof(ListingLocationStateResponse).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "Latitude",
                "Longitude",
                "LocationPrecision",
                "GeocodedDisplayName",
                "LocationConfirmedAtUtc");
        typeof(ConfirmListingLocationCommand).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "ListingId",
                "ConfirmationToken");
    }

    [Fact]
    public async Task DomainValidationFailureDisposesScopeWithoutSaveOrCommit()
    {
        var context = new TestContext();
        context.Geocoder.Result = GeocodingResolutionResult.Success(
            new ResolvedGeocodingSnapshot(
                "provider",
                "opaque-reference",
                999m,
                21.42m,
                LocationPrecision.City,
                null));

        Func<Task> act = async () => await context.HandleAsync();

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        context.WriteScope.SaveCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.LockedListing.Latitude.Should().BeNull();
    }

    [Fact]
    public async Task PersistenceFailureDisposesUncommittedScope()
    {
        var context = new TestContext();
        context.WriteScope.SaveException =
            new InvalidOperationException("Simulated persistence failure.");

        Func<Task> act = async () => await context.HandleAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CallerCancellationFromProviderPropagatesWithoutWriteScope()
    {
        var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.Geocoder.Exception =
            new OperationCanceledException(cancellation.Token);

        Func<Task> act = async () => await context.Handler.HandleAsync(
            new ConfirmListingLocationCommand(
                context.ReadListing.Id,
                "opaque-token"),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        context.ListingRepository.BeginCallCount.Should().Be(0);
    }

    [Fact]
    public async Task CallerCancellationDuringSavePropagatesAndDisposesScope()
    {
        var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.WriteScope.SaveException =
            new OperationCanceledException(cancellation.Token);

        Func<Task> act = async () => await context.Handler.HandleAsync(
            new ConfirmListingLocationCommand(
                context.ReadListing.Id,
                "opaque-token"),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
    }

    private static void AssertLockedRejection(
        TestContext context,
        ServiceResult<ListingLocationStateResponse> result,
        string expectedCode)
    {
        result.Status.Should().NotBe(ServiceResultStatus.Success);
        result.ErrorCode.Should().Be(expectedCode);
        context.Geocoder.CallCount.Should().Be(1);
        context.ListingRepository.BeginCallCount.Should().Be(1);
        context.WriteScope.SaveCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.LockedListing.Latitude.Should().BeNull();
        context.LockedListing.GeocodingProviderKey.Should().BeNull();
    }

    private static string Fingerprint(Listing listing)
    {
        return ListingLocationFingerprint.Compute(
            CanonicalListingLocation.From(listing.Translations.Select(
                translation => new CanonicalListingLocationInput(
                    translation.LanguageCode,
                    translation.City,
                    translation.Municipality,
                    translation.AddressLine,
                    translation.Neighborhood))));
    }

    private static Listing CreateDraft(
        Guid listingId,
        Guid creatorId)
    {
        var listing = new Listing
        {
            Id = listingId,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 100_000m,
            Currency = "EUR",
            AreaSquareMeters = 70m
        };
        listing.AssignCreator(creatorId);
        listing.Translations.Add(new ListingTranslation
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            Listing = listing,
            LanguageCode = "en",
            Title = "Listing",
            Description = "Description",
            City = "Skopje",
            Municipality = "Centar",
            AddressLine = "Macedonia Street 10",
            Neighborhood = null
        });
        return listing;
    }

    private static InvalidOperationException UnexpectedCall(string memberName)
    {
        return new InvalidOperationException($"Unexpected call to {memberName}.");
    }

    private sealed class TestContext
    {
        public TestContext(UserStatus userStatus = UserStatus.Active)
        {
            Calls = [];
            Actor = new User(
                "author@example.com",
                "password-hash",
                "Test",
                "Author",
                null,
                status: userStatus);
            Guid listingId = Guid.NewGuid();
            ReadListing = CreateDraft(listingId, Actor.Id);
            LockedListing = CreateDraft(listingId, Actor.Id);
            CurrentUser = new FakeCurrentUserService { UserId = Actor.Id };
            UserRepository = new FakeUserRepository(Calls);
            UserRepository.SetResults(Actor, Actor);
            WriteScope = new FakeWriteScope(LockedListing, Calls);
            ListingRepository = new FakeListingAuthoringRepository(
                ReadListing,
                WriteScope,
                Calls);
            AgencyRepository = new FakeAgencyRepository(Calls);
            Geocoder = new FakeGeocoder(Calls)
            {
                Result = GeocodingResolutionResult.Success(
                    new ResolvedGeocodingSnapshot(
                        "provider",
                        "opaque-reference",
                        41.99m,
                        21.42m,
                        LocationPrecision.City,
                        "Resolved"))
            };
            TokenProtector = new FakeTokenProtector(Calls);
            SetToken();
            Handler = new ConfirmListingLocationHandler(
                ListingRepository,
                UserRepository,
                new AgencyListingAccessChecker(AgencyRepository),
                CurrentUser,
                Geocoder,
                TokenProtector,
                new FixedTimeProvider(ConfirmedAt));
        }

        public List<string> Calls { get; }
        public User Actor { get; }
        public Listing ReadListing { get; }
        public Listing LockedListing { get; }
        public FakeCurrentUserService CurrentUser { get; }
        public FakeUserRepository UserRepository { get; }
        public FakeListingAuthoringRepository ListingRepository { get; }
        public FakeWriteScope WriteScope { get; }
        public FakeAgencyRepository AgencyRepository { get; }
        public FakeGeocoder Geocoder { get; }
        public FakeTokenProtector TokenProtector { get; }
        public ConfirmListingLocationHandler Handler { get; }

        public void SetToken(
            Guid? listingId = null,
            Guid? actorUserId = null)
        {
            LocationConfirmationTokenClaims claims =
                LocationConfirmationTokenClaims.Create(
                    listingId ?? ReadListing.Id,
                    actorUserId ?? Actor.Id,
                    "provider",
                    "opaque-reference",
                    "en",
                    Fingerprint(ReadListing));
            LocationConfirmationTokenPayload payload =
                LocationConfirmationTokenPayload.CreateCurrent(
                    claims,
                    ConfirmedAt.AddMinutes(-1),
                    ConfirmedAt.AddMinutes(9));
            TokenProtector.Result =
                LocationConfirmationTokenUnprotectResult.Success(payload);
        }

        public void AssignAgency(
            AgencyMemberRole role,
            AgencyMemberStatus memberStatus,
            AgencyStatus agencyStatus = AgencyStatus.Active)
        {
            var agency = new Agency(
                "Agency",
                "agency",
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            if (agencyStatus == AgencyStatus.Active)
            {
                agency.Approve();
            }
            else if (agencyStatus == AgencyStatus.Disabled)
            {
                agency.Disable();
            }

            ReadListing.AssignAgency(agency.Id);
            LockedListing.AssignAgency(agency.Id);
            AgencyRepository.SetAgencyResults(agency, agency);
            AgencyRepository.SetMemberResults(
                new AgencyMemberAccessReadModel
                {
                    Role = role,
                    Status = memberStatus
                },
                new AgencyMemberAccessReadModel
                {
                    Role = role,
                    Status = memberStatus
                });
        }

        public Task<ServiceResult<ListingLocationStateResponse>> HandleAsync()
        {
            return Handler.HandleAsync(
                new ConfirmListingLocationCommand(
                    ReadListing.Id,
                    "opaque-token"),
                CancellationToken.None);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }

        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<string> _calls;
        private readonly Queue<User?> _results = new();
        private User? _lastResult;

        public FakeUserRepository(List<string> calls)
        {
            _calls = calls;
        }

        public int CallCount { get; private set; }

        public void SetResults(params User?[] results)
        {
            _results.Clear();
            foreach (User? result in results)
            {
                _results.Enqueue(result);
            }
            _lastResult = results.LastOrDefault();
        }

        public Task<User?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken)
        {
            _calls.Add("user.get");
            CallCount++;
            User? result = _results.Count > 0 ? _results.Dequeue() : _lastResult;
            return Task.FromResult(result);
        }

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(ExistsByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByNormalizedEmailReadOnlyAsync));
        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task AddAsync(User user, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(AddAsync));
        public Task<UserRegistrationPersistenceResult> PersistRegistrationAsync(User user, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(PersistRegistrationAsync));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SaveChangesAsync));
    }

    private sealed class FakeListingAuthoringRepository : IListingAuthoringRepository
    {
        private readonly List<string> _calls;
        private readonly FakeWriteScope _writeScope;

        public FakeListingAuthoringRepository(
            Listing readListing,
            FakeWriteScope writeScope,
            List<string> calls)
        {
            ReadResult = readListing;
            _writeScope = writeScope;
            _calls = calls;
        }

        public Listing? ReadResult { get; set; }
        public bool ReturnMissingWriteScope { get; set; }
        public int ReadCallCount { get; private set; }
        public int BeginCallCount { get; private set; }

        public Task<Listing?> GetByIdReadOnlyAsync(Guid listingId, CancellationToken cancellationToken)
        {
            _calls.Add("listing.read");
            ReadCallCount++;
            return Task.FromResult(ReadResult);
        }

        public Task<IListingAuthoringWriteScope?> BeginWriteAsync(Guid listingId, CancellationToken cancellationToken)
        {
            _calls.Add("listing.write.begin");
            BeginCallCount++;
            return Task.FromResult<IListingAuthoringWriteScope?>(
                ReturnMissingWriteScope ? null : _writeScope);
        }
    }

    private sealed class FakeWriteScope : IListingAuthoringWriteScope
    {
        private readonly List<string> _calls;

        public FakeWriteScope(Listing listing, List<string> calls)
        {
            Listing = listing;
            _calls = calls;
        }

        public Listing Listing { get; }
        public Exception? SaveException { get; set; }
        public int SaveCallCount { get; private set; }
        public int CommitCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            _calls.Add("listing.save");
            SaveCallCount++;
            return SaveException is null
                ? Task.CompletedTask
                : Task.FromException(SaveException);
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            _calls.Add("listing.commit");
            CommitCallCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _calls.Add("listing.dispose");
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }

        public void AddTranslation(ListingTranslation translation) => throw UnexpectedCall(nameof(AddTranslation));
        public void RemoveTranslation(ListingTranslation translation) => throw UnexpectedCall(nameof(RemoveTranslation));
        public void MarkListingModified() => throw UnexpectedCall(nameof(MarkListingModified));
    }

    private sealed class FakeGeocoder : IListingGeocoder
    {
        private readonly List<string> _calls;

        public FakeGeocoder(List<string> calls)
        {
            _calls = calls;
        }

        public GeocodingResolutionResult Result { get; set; } = default!;
        public Exception? Exception { get; set; }
        public GeocodingReference? Reference { get; private set; }
        public int CallCount { get; private set; }

        public Task<GeocodingResolutionResult> ResolveAsync(GeocodingReference reference, CancellationToken cancellationToken)
        {
            _calls.Add("provider.resolve");
            Reference = reference;
            CallCount++;
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<GeocodingResolutionResult>(Exception);
        }

        public Task<GeocodingSearchResult> SearchAsync(GeocodingSearchInput input, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SearchAsync));
    }

    private sealed class FakeTokenProtector : ILocationConfirmationTokenProtector
    {
        private readonly List<string> _calls;

        public FakeTokenProtector(List<string> calls)
        {
            _calls = calls;
        }

        public LocationConfirmationTokenUnprotectResult Result { get; set; } = default!;
        public int UnprotectCallCount { get; private set; }

        public LocationConfirmationTokenUnprotectResult Unprotect(string protectedToken)
        {
            _calls.Add("token.unprotect");
            UnprotectCallCount++;
            return Result;
        }

        public string Protect(LocationConfirmationTokenClaims claims) => throw UnexpectedCall(nameof(Protect));
    }

    private sealed class FakeAgencyRepository : IAgencyRepository
    {
        private readonly List<string> _calls;
        private readonly Queue<Agency?> _agencyResults = new();
        private readonly Queue<AgencyMemberAccessReadModel?> _memberResults = new();
        private Agency? _lastAgency;
        private AgencyMemberAccessReadModel? _lastMember;

        public FakeAgencyRepository(List<string> calls)
        {
            _calls = calls;
        }

        public int MemberCallCount { get; private set; }

        public void SetAgencyResults(params Agency?[] results)
        {
            _agencyResults.Clear();
            foreach (Agency? result in results) _agencyResults.Enqueue(result);
            _lastAgency = results.LastOrDefault();
        }

        public void SetMemberResults(params AgencyMemberAccessReadModel?[] results)
        {
            _memberResults.Clear();
            foreach (AgencyMemberAccessReadModel? result in results) _memberResults.Enqueue(result);
            _lastMember = results.LastOrDefault();
        }

        public Task<Agency?> GetByIdReadOnlyAsync(Guid agencyId, CancellationToken cancellationToken)
        {
            _calls.Add("agency.get");
            Agency? result = _agencyResults.Count > 0 ? _agencyResults.Dequeue() : _lastAgency;
            return Task.FromResult(result);
        }

        public Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(Guid agencyId, Guid userId, CancellationToken cancellationToken)
        {
            _calls.Add("agency.member.get");
            MemberCallCount++;
            AgencyMemberAccessReadModel? result = _memberResults.Count > 0 ? _memberResults.Dequeue() : _lastMember;
            return Task.FromResult(result);
        }

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
