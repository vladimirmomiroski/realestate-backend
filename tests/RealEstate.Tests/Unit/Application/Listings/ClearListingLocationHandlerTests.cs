using FluentAssertions;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.ClearListingLocation;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class ClearListingLocationHandlerTests
{
    [Fact]
    public async Task MissingPrincipalFailsBeforeUserOrWriteScope()
    {
        var context = new TestContext();
        context.CurrentUser.UserId = null;

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.AuthenticationInvalidPrincipal);
        context.UserRepository.CallCount.Should().Be(0);
        context.ListingRepository.BeginCallCount.Should().Be(0);
        AssertConfirmedLocationUnchanged(context);
    }

    [Fact]
    public async Task MissingDatabaseUserFailsBeforeWriteScope()
    {
        var context = new TestContext();
        context.UserRepository.SetResults((User?)null);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.AuthenticationInvalidPrincipal);
        context.ListingRepository.BeginCallCount.Should().Be(0);
        AssertConfirmedLocationUnchanged(context);
    }

    [Fact]
    public async Task DisabledUserFailsBeforeWriteScope()
    {
        var context = new TestContext(UserStatus.Disabled);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationAccountDisabled);
        context.ListingRepository.BeginCallCount.Should().Be(0);
        AssertConfirmedLocationUnchanged(context);
    }

    [Fact]
    public async Task MissingWriteScopeReturnsNotFoundWithoutMutation()
    {
        var context = new TestContext();
        context.ListingRepository.ReturnMissingWriteScope = true;

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.ResourceNotFound);
        context.ListingRepository.BeginCallCount.Should().Be(1);
        context.WriteScope.DisposeCallCount.Should().Be(0);
        AssertConfirmedLocationUnchanged(context);
    }

    [Fact]
    public async Task UserDeletedWhileWaitingForLockRejectsWithoutMutation()
    {
        var context = new TestContext();
        context.UserRepository.SetResults(context.Actor, null);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Unauthorized);
        result.ErrorCode.Should().Be(ErrorCodes.AuthenticationInvalidPrincipal);
        AssertLockedFailure(context);
    }

    [Fact]
    public async Task UserDisabledWhileWaitingForLockRejectsWithoutMutation()
    {
        var context = new TestContext();
        var disabled = new User(
            "disabled@example.com",
            "password-hash",
            "Disabled",
            "User",
            null,
            status: UserStatus.Disabled);
        context.UserRepository.SetResults(context.Actor, disabled);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationAccountDisabled);
        AssertLockedFailure(context);
    }

    [Fact]
    public async Task PersonalNonownerIsForbiddenWithoutMutation()
    {
        var context = new TestContext();
        context.Listing.AssignCreator(Guid.NewGuid());

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        AssertLockedFailure(context);
    }

    [Theory]
    [InlineData(AgencyMemberRole.Manager, AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Disabled)]
    public async Task UnauthorizedAgencyMembershipDoesNotClear(
        AgencyMemberRole role,
        AgencyMemberStatus status)
    {
        var context = new TestContext();
        context.AssignAgency(role, status);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        AssertLockedFailure(context);
    }

    [Fact]
    public async Task AgencyNonmemberIsForbiddenWithoutMutation()
    {
        var context = new TestContext();
        context.AssignAgency(
            AgencyMemberRole.Owner,
            AgencyMemberStatus.Active);
        context.AgencyRepository.Member = null;

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        AssertLockedFailure(context);
    }

    [Fact]
    public async Task NonDraftListingIsRejectedWithoutMutation()
    {
        var context = new TestContext();
        context.Listing.Publish().IsReady.Should().BeTrue();

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictResourceState);
        context.Listing.Status.Should().Be(ListingStatus.Active);
        AssertLockedFailure(context);
    }

    [Fact]
    public async Task PersonalCreatorClearsAllLocationStateAndCommits()
    {
        var context = new TestContext();
        decimal originalPrice = context.Listing.Price;
        string originalAddress =
            context.Listing.Translations.Single().AddressLine!;

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().Be(new ListingLocationStateResponse(
            null,
            null,
            null,
            null,
            null));
        AssertAllLocationStateIsNull(context.Listing);
        context.Listing.Price.Should().Be(originalPrice);
        context.Listing.Status.Should().Be(ListingStatus.Draft);
        context.Listing.Translations.Single().AddressLine
            .Should().Be(originalAddress);
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(1);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.UserRepository.CallCount.Should().Be(2);
        context.Calls.Should().ContainInOrder(
            "user.get",
            "listing.write.begin",
            "user.get",
            "listing.save",
            "listing.commit",
            "listing.dispose");
    }

    [Theory]
    [InlineData(AgencyMemberRole.Owner)]
    [InlineData(AgencyMemberRole.Agent)]
    public async Task ActiveAgencyOwnerOrAgentCanClear(
        AgencyMemberRole role)
    {
        var context = new TestContext();
        context.AssignAgency(role, AgencyMemberStatus.Active);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        AssertAllLocationStateIsNull(context.Listing);
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Disabled)]
    public async Task PendingVerificationUserCanClearForNonActiveAgency(
        AgencyStatus agencyStatus)
    {
        var context = new TestContext(UserStatus.PendingVerification);
        context.AssignAgency(
            AgencyMemberRole.Owner,
            AgencyMemberStatus.Active,
            agencyStatus);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        AssertAllLocationStateIsNull(context.Listing);
    }

    [Fact]
    public async Task LegacyPairedCoordinatesAreClearedCompletely()
    {
        var context = new TestContext();
        context.Listing.ClearLocation();
        SetPrivateProperty(context.Listing, nameof(Listing.Latitude), 41.99m);
        SetPrivateProperty(context.Listing, nameof(Listing.Longitude), 21.42m);

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        AssertAllLocationStateIsNull(context.Listing);
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(1);
    }

    [Fact]
    public async Task AlreadyUnresolvedDraftClearIsIdempotent()
    {
        var context = new TestContext();
        context.Listing.ClearLocation();

        ServiceResult<ListingLocationStateResponse> result =
            await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().Be(new ListingLocationStateResponse(
            null,
            null,
            null,
            null,
            null));
        AssertAllLocationStateIsNull(context.Listing);
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(1);
        context.WriteScope.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PersistenceFailureDisposesWithoutCommit()
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
    public async Task CancellationDuringSavePropagatesAndDisposesWithoutCommit()
    {
        var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.WriteScope.SaveException =
            new OperationCanceledException(cancellation.Token);

        Func<Task> act = async () => await context.Handler.HandleAsync(
            new ClearListingLocationCommand(context.Listing.Id),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        context.WriteScope.SaveCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public void ContractsContainNoProviderTokenOrLocationInput()
    {
        typeof(ClearListingLocationCommand).GetProperties()
            .Select(property => property.Name)
            .Should().Equal("ListingId");

        typeof(ClearListingLocationHandler).GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().Equal(
            [
                typeof(IListingAuthoringRepository),
                typeof(IUserRepository),
                typeof(AgencyListingAccessChecker),
                typeof(ICurrentUserService)
            ]);
    }

    private static void AssertLockedFailure(TestContext context)
    {
        context.WriteScope.SaveCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        AssertConfirmedLocationUnchanged(context);
    }

    private static void AssertConfirmedLocationUnchanged(TestContext context)
    {
        context.Listing.Latitude.Should().Be(41.99m);
        context.Listing.Longitude.Should().Be(21.42m);
        context.Listing.LocationPrecision.Should().Be(LocationPrecision.City);
        context.Listing.GeocodingProviderKey.Should().Be("provider");
        context.Listing.GeocodingResultReference.Should().Be("reference");
        context.Listing.GeocodedDisplayName.Should().Be("Display");
        context.Listing.LocationConfirmedAtUtc.Should().Be(
            new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc));
        context.WriteScope.SaveCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
    }

    private static void AssertAllLocationStateIsNull(Listing listing)
    {
        listing.Latitude.Should().BeNull();
        listing.Longitude.Should().BeNull();
        listing.LocationPrecision.Should().BeNull();
        listing.GeocodingProviderKey.Should().BeNull();
        listing.GeocodingResultReference.Should().BeNull();
        listing.GeocodedDisplayName.Should().BeNull();
        listing.LocationConfirmedAtUtc.Should().BeNull();
    }

    private static void SetPrivateProperty<T>(
        Listing listing,
        string propertyName,
        T value)
    {
        typeof(Listing).GetProperty(propertyName)!
            .SetValue(listing, value);
    }

    private static InvalidOperationException UnexpectedCall(string memberName)
    {
        return new InvalidOperationException(
            $"Unexpected call to {memberName}.");
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
            Listing = CreateConfirmedDraft(Guid.NewGuid(), Actor.Id);
            CurrentUser = new FakeCurrentUserService { UserId = Actor.Id };
            UserRepository = new FakeUserRepository(Calls);
            UserRepository.SetResults(Actor, Actor);
            WriteScope = new FakeWriteScope(Listing, Calls);
            ListingRepository = new FakeListingAuthoringRepository(
                WriteScope,
                Calls);
            AgencyRepository = new FakeAgencyRepository(Calls);
            Handler = new ClearListingLocationHandler(
                ListingRepository,
                UserRepository,
                new AgencyListingAccessChecker(AgencyRepository),
                CurrentUser);
        }

        public List<string> Calls { get; }
        public User Actor { get; }
        public Listing Listing { get; }
        public FakeCurrentUserService CurrentUser { get; }
        public FakeUserRepository UserRepository { get; }
        public FakeListingAuthoringRepository ListingRepository { get; }
        public FakeWriteScope WriteScope { get; }
        public FakeAgencyRepository AgencyRepository { get; }
        public ClearListingLocationHandler Handler { get; }

        public Task<ServiceResult<ListingLocationStateResponse>> HandleAsync()
        {
            return Handler.HandleAsync(
                new ClearListingLocationCommand(Listing.Id),
                CancellationToken.None);
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

            Listing.AssignAgency(agency.Id);
            AgencyRepository.Agency = agency;
            AgencyRepository.Member = new AgencyMemberAccessReadModel
            {
                Role = role,
                Status = memberStatus
            };
        }
    }

    private static Listing CreateConfirmedDraft(
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
            AddressLine = "Macedonia Street 10"
        });
        listing.ConfirmLocation(
            41.99m,
            21.42m,
            LocationPrecision.City,
            "provider",
            "reference",
            "Display",
            new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc));
        return listing;
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

        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            _calls.Add("user.get");
            CallCount++;
            User? result = _results.Count > 0
                ? _results.Dequeue()
                : _lastResult;
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

    private sealed class FakeListingAuthoringRepository
        : IListingAuthoringRepository
    {
        private readonly FakeWriteScope _writeScope;
        private readonly List<string> _calls;

        public FakeListingAuthoringRepository(
            FakeWriteScope writeScope,
            List<string> calls)
        {
            _writeScope = writeScope;
            _calls = calls;
        }

        public bool ReturnMissingWriteScope { get; set; }
        public int BeginCallCount { get; private set; }

        public Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            _calls.Add("listing.write.begin");
            BeginCallCount++;
            return Task.FromResult<IListingAuthoringWriteScope?>(
                ReturnMissingWriteScope ? null : _writeScope);
        }

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid listingId,
            CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByIdReadOnlyAsync));
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

        public void AddTranslation(ListingTranslation translation) =>
            throw UnexpectedCall(nameof(AddTranslation));

        public void RemoveTranslation(ListingTranslation translation) =>
            throw UnexpectedCall(nameof(RemoveTranslation));

        public void MarkListingModified() =>
            throw UnexpectedCall(nameof(MarkListingModified));
    }

    private sealed class FakeAgencyRepository : IAgencyRepository
    {
        private readonly List<string> _calls;

        public FakeAgencyRepository(List<string> calls)
        {
            _calls = calls;
        }

        public Agency? Agency { get; set; }
        public AgencyMemberAccessReadModel? Member { get; set; }

        public Task<Agency?> GetByIdReadOnlyAsync(
            Guid agencyId,
            CancellationToken cancellationToken)
        {
            _calls.Add("agency.get");
            return Task.FromResult(Agency);
        }

        public Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(
            Guid agencyId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            _calls.Add("agency.member.get");
            return Task.FromResult(Member);
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
