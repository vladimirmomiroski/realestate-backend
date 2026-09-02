using FluentAssertions;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.PublishListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class PublishListingHandlerTests
{
    [Fact]
    public async Task Handle_IncompleteAuthorizedDraft_DoesNotSaveOrCommit()
    {
        var context = new TestContext();
        context.Listing.Translations.Single().Description = null;

        ServiceResult<PublicListingResponse> result = await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictListingNotReady);
        context.Listing.Status.Should().Be(ListingStatus.Draft);
        context.WriteScope.SaveChangesCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.Calls.Should().Equal(
            "current-user.id",
            "user.get",
            "listing.scope.begin",
            "listing.scope.dispose");
    }

    [Fact]
    public async Task Handle_MalformedAuthorizedActive_DoesNotSaveOrCommit()
    {
        var context = new TestContext();
        context.Listing.Publish().IsReady.Should().BeTrue();
        context.Listing.ClearLocation();

        ServiceResult<PublicListingResponse> result = await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictListingNotReady);
        context.Listing.Status.Should().Be(ListingStatus.Active);
        context.WriteScope.SaveChangesCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_IncompletePersonalDraftButNonOwner_AuthorizesBeforeReadiness()
    {
        var context = new TestContext();
        context.Listing.AssignCreator(Guid.NewGuid());
        context.Listing.ClearLocation();

        ServiceResult<PublicListingResponse> result = await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        context.Listing.Status.Should().Be(ListingStatus.Draft);
        context.WriteScope.SaveChangesCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_IncompleteAgencyDraftButNonMember_AuthorizesBeforeReadiness()
    {
        var context = new TestContext();
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
        agency.Approve();
        context.Listing.AssignAgency(agency.Id);
        context.Listing.ClearLocation();
        context.AgencyRepository.AgencyResult = agency;
        context.AgencyRepository.MemberAccessResult = null;

        ServiceResult<PublicListingResponse> result = await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
        context.Listing.Status.Should().Be(ListingStatus.Draft);
        context.WriteScope.SaveChangesCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.Calls.Should().Equal(
            "current-user.id",
            "user.get",
            "listing.scope.begin",
            "agency.get",
            "agency.member.get",
            "listing.scope.dispose");
    }

    [Fact]
    public async Task Handle_InvalidConfirmedLocation_ReturnsFixedSanitizedConflict()
    {
        const string ProviderPayload = " private-provider-payload ";
        var context = new TestContext();
        typeof(Listing)
            .GetProperty(nameof(Listing.GeocodingProviderKey))!
            .SetValue(context.Listing, ProviderPayload);

        ServiceResult<PublicListingResponse> result = await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.ErrorCode.Should().Be(ErrorCodes.ConflictListingNotReady);
        result.Error.Should().Be(
            "The listing is not ready for publication.");
        result.Error.Should().NotContain(ProviderPayload);
        context.Listing.Status.Should().Be(ListingStatus.Draft);
        context.WriteScope.SaveChangesCallCount.Should().Be(0);
        context.WriteScope.CommitCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReadyAuthorizedDraft_SavesAndCommitsExactlyOnce()
    {
        var context = new TestContext();

        ServiceResult<PublicListingResponse> result = await context.HandleAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().NotBeNull();
        result.Value!.LanguageCode.Should().Be("en");
        result.Value.Title.Should().Be("Ready listing");
        result.Value.City.Should().Be("Skopje");
        result.Value.Description.Should().Be(
            "Complete publication content.");
        context.Listing.Status.Should().Be(ListingStatus.Active);
        context.WriteScope.SaveChangesCallCount.Should().Be(1);
        context.WriteScope.CommitCallCount.Should().Be(1);
        context.WriteScope.DisposeCallCount.Should().Be(1);
        context.Calls.Should().Equal(
            "current-user.id",
            "user.get",
            "listing.scope.begin",
            "listing.save",
            "listing.scope.commit",
            "listing.scope.dispose");
    }

    private static InvalidOperationException UnexpectedCall(string memberName)
    {
        return new InvalidOperationException($"Unexpected call to {memberName}.");
    }

    private sealed class TestContext
    {
        public TestContext()
        {
            Calls = [];
            Actor = new User(
                "publisher@example.com",
                "password-hash",
                "Test",
                "Publisher",
                phoneNumber: null,
                status: UserStatus.Active);
            Listing = CreateReadyDraft(Actor.Id);
            WriteScope = new FakeListingAuthoringWriteScope(Listing, Calls);
            ListingRepository = new FakeListingAuthoringRepository(WriteScope, Calls);
            UserRepository = new FakeUserRepository(Actor, Calls);
            AgencyRepository = new FakeAgencyRepository(Calls);
            CurrentUser = new FakeCurrentUserService(Actor.Id, Calls);
            Handler = new PublishListingHandler(
                ListingRepository,
                UserRepository,
                new AgencyListingAccessChecker(AgencyRepository),
                CurrentUser);
        }

        public List<string> Calls { get; }
        public User Actor { get; }
        public Listing Listing { get; }
        public FakeListingAuthoringWriteScope WriteScope { get; }
        public FakeListingAuthoringRepository ListingRepository { get; }
        public FakeUserRepository UserRepository { get; }
        public FakeAgencyRepository AgencyRepository { get; }
        public FakeCurrentUserService CurrentUser { get; }
        public PublishListingHandler Handler { get; }

        public Task<ServiceResult<PublicListingResponse>> HandleAsync()
        {
            return Handler.HandleAsync(
                new PublishListingCommand(Listing.Id, "en"),
                CancellationToken.None);
        }

        private static Listing CreateReadyDraft(Guid creatorId)
        {
            Listing listing = StrongLocationListingTestFixtures
                .CreatePublishableConfirmedDraft();
            ListingTranslation translation = listing.Translations
                .Single(item => item.LanguageCode == "en");
            translation.Title = "Ready listing";
            translation.Description = "Complete publication content.";
            listing.Translations = [translation];
            listing.AssignCreator(creatorId);
            return listing;
        }
    }

    private sealed class FakeListingAuthoringRepository
        : IListingAuthoringRepository
    {
        private readonly IListingAuthoringWriteScope _writeScope;
        private readonly List<string> _calls;

        public FakeListingAuthoringRepository(
            IListingAuthoringWriteScope writeScope,
            List<string> calls)
        {
            _writeScope = writeScope;
            _calls = calls;
        }

        public Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            _calls.Add("listing.scope.begin");
            return Task.FromResult<IListingAuthoringWriteScope?>(_writeScope);
        }

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(nameof(GetByIdReadOnlyAsync));
        }
    }

    private sealed class FakeListingAuthoringWriteScope
        : IListingAuthoringWriteScope
    {
        private readonly List<string> _calls;

        public FakeListingAuthoringWriteScope(Listing listing, List<string> calls)
        {
            Listing = listing;
            _calls = calls;
        }

        public Listing Listing { get; }
        public int SaveChangesCallCount { get; private set; }
        public int CommitCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            _calls.Add("listing.save");
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            _calls.Add("listing.scope.commit");
            CommitCallCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _calls.Add("listing.scope.dispose");
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }

        public void AddTranslation(ListingTranslation translation)
        {
            throw UnexpectedCall(nameof(AddTranslation));
        }

        public void RemoveTranslation(ListingTranslation translation)
        {
            throw UnexpectedCall(nameof(RemoveTranslation));
        }

        public void MarkListingModified()
        {
            throw UnexpectedCall(nameof(MarkListingModified));
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;
        private readonly List<string> _calls;

        public FakeUserRepository(User user, List<string> calls)
        {
            _user = user;
            _calls = calls;
        }

        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            _calls.Add("user.get");
            return Task.FromResult<User?>(_user);
        }

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(ExistsByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByNormalizedEmailReadOnlyAsync));
        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task AddAsync(User user, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(AddAsync));
        public Task<UserRegistrationPersistenceResult> PersistRegistrationAsync(User user, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(PersistRegistrationAsync));
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(SaveChangesAsync));
    }

    private sealed class FakeAgencyRepository : IAgencyRepository
    {
        private readonly List<string> _calls;

        public FakeAgencyRepository(List<string> calls)
        {
            _calls = calls;
        }

        public Agency? AgencyResult { get; set; }
        public AgencyMemberAccessReadModel? MemberAccessResult { get; set; }

        public Task<Agency?> GetByIdReadOnlyAsync(Guid agencyId, CancellationToken cancellationToken)
        {
            _calls.Add("agency.get");
            return Task.FromResult(AgencyResult);
        }

        public Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(
            Guid agencyId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            _calls.Add("agency.member.get");
            return Task.FromResult(MemberAccessResult);
        }

        public Task<AgencyCreationPersistenceResult> CreateAsync(Agency agency, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(CreateAsync));
        public Task<Agency?> GetBySlugReadOnlyAsync(string slug, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetBySlugReadOnlyAsync));
        public Task<Agency?> GetByIdForUpdateAsync(Guid agencyId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task<Agency?> GetByIdWithMembersForUpdateAsync(Guid agencyId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByIdWithMembersForUpdateAsync));
        public void AddMember(AgencyMember member) => throw UnexpectedCall(nameof(AddMember));
        public Task<IAgencyOwnerMutationScope?> BeginLastActiveOwnerMutationAsync(Guid agencyId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(BeginLastActiveOwnerMutationAsync));
        public Task<AgencyMember?> GetMemberByIdForUpdateAsync(Guid agencyId, Guid memberId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetMemberByIdForUpdateAsync));
        public Task<AgencyDashboardSummaryReadModel?> GetDashboardSummaryReadOnlyAsync(Guid agencyId, DateTime utcNow, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetDashboardSummaryReadOnlyAsync));
        public Task<int> CountActiveOwnersAsync(Guid agencyId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(CountActiveOwnersAsync));
        public Task<IReadOnlyList<UserAgencyMembershipReadModel>> GetByUserIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetByUserIdReadOnlyAsync));
        public Task<IReadOnlyList<AgencyMemberReadModel>> GetMembersByAgencyIdReadOnlyAsync(Guid agencyId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(GetMembersByAgencyIdReadOnlyAsync));
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(SlugExistsAsync));
        public Task<bool> ExistsAsync(Guid agencyId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(ExistsAsync));
        public Task<bool> IsActiveMemberAsync(Guid agencyId, Guid userId, CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(IsActiveMemberAsync));
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw UnexpectedCall(nameof(SaveChangesAsync));
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;
        private readonly List<string> _calls;

        public FakeCurrentUserService(Guid userId, List<string> calls)
        {
            _userId = userId;
            _calls = calls;
        }

        public Guid? UserId
        {
            get
            {
                _calls.Add("current-user.id");
                return _userId;
            }
        }

        public bool IsAuthenticated => true;
    }
}
