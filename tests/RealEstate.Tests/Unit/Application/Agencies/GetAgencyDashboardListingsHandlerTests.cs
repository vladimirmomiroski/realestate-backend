using FluentAssertions;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Agencies;

public sealed class GetAgencyDashboardListingsHandlerTests
{
    [Fact]
    public async Task Handle_UndefinedStatusAfterSuccessfulAccess_ReturnsCanonicalValidationWithoutRepositoryCall()
    {
        HandlerFixture fixture = CreateFixture();

        ServiceResult<PagedResponse<ListingResponse>> result =
            await fixture.Handler.HandleAsync(
                CreateQuery((ListingStatus)999),
                CancellationToken.None);

        result.Status.Should().Be(ServiceResultStatus.ValidationError);
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        result.ValidationKey.Should().Be("status");
        result.Error.Should().Be(
            GetAgencyDashboardListingsValidator.UndefinedStatusError);
        fixture.Calls.Should().Equal(
            "user.get",
            "agency.get",
            "agency.member.get");
        fixture.ListingRepository.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(
        "unauthenticated",
        ServiceResultStatus.Unauthorized,
        ErrorCodes.AuthenticationInvalidPrincipal)]
    [InlineData(
        "missing-account",
        ServiceResultStatus.Unauthorized,
        ErrorCodes.AuthenticationInvalidPrincipal)]
    [InlineData(
        "disabled-account",
        ServiceResultStatus.Forbidden,
        ErrorCodes.AuthorizationAccountDisabled)]
    [InlineData(
        "missing-agency",
        ServiceResultStatus.NotFound,
        ErrorCodes.ResourceNotFound)]
    [InlineData(
        "inactive-member",
        ServiceResultStatus.Forbidden,
        ErrorCodes.AuthorizationForbidden)]
    [InlineData(
        "manager-role",
        ServiceResultStatus.Forbidden,
        ErrorCodes.AuthorizationForbidden)]
    public async Task Handle_UndefinedStatus_PreservesAccessFailurePrecedence(
        string scenario,
        ServiceResultStatus expectedStatus,
        string expectedErrorCode)
    {
        HandlerFixture fixture = CreateFixture(scenario);

        ServiceResult<PagedResponse<ListingResponse>> result =
            await fixture.Handler.HandleAsync(
                CreateQuery((ListingStatus)999),
                CancellationToken.None);

        result.Status.Should().Be(expectedStatus);
        result.ErrorCode.Should().Be(expectedErrorCode);
        result.ValidationKey.Should().BeNull();
        fixture.Calls.Should().Equal(ExpectedCalls(scenario));
        fixture.ListingRepository.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DefinedStatusAfterSuccessfulAccess_CallsRepositoryInProtectedOrder()
    {
        HandlerFixture fixture = CreateFixture();

        ServiceResult<PagedResponse<ListingResponse>> result =
            await fixture.Handler.HandleAsync(
                CreateQuery(ListingStatus.Archived),
                CancellationToken.None);

        result.Status.Should().Be(ServiceResultStatus.Success);
        fixture.ListingRepository.CallCount.Should().Be(1);
        fixture.ListingRepository.Status.Should().Be(ListingStatus.Archived);
        fixture.Calls.Should().Equal(
            "user.get",
            "agency.get",
            "agency.member.get",
            "listing.dashboard.get");
    }

    private static GetAgencyDashboardListingsQuery CreateQuery(
        ListingStatus? status)
    {
        return new GetAgencyDashboardListingsQuery
        {
            AgencyId = Guid.NewGuid(),
            Status = status,
            Page = 1,
            PageSize = 20
        };
    }

    private static HandlerFixture CreateFixture(string? scenario = null)
    {
        var calls = new List<string>();
        var currentUser = new FakeCurrentUserService
        {
            IsAuthenticated = scenario != "unauthenticated",
            UserId = scenario == "unauthenticated"
                ? null
                : Guid.NewGuid()
        };
        User? user = scenario switch
        {
            "missing-account" => null,
            "disabled-account" => CreateUser(UserStatus.Disabled),
            _ => CreateUser(UserStatus.Active)
        };
        Agency? agency = scenario == "missing-agency"
            ? null
            : CreateAgency();
        AgencyMemberAccessReadModel? member = scenario switch
        {
            "inactive-member" => new AgencyMemberAccessReadModel
            {
                Role = AgencyMemberRole.Agent,
                Status = AgencyMemberStatus.Disabled
            },
            "manager-role" => new AgencyMemberAccessReadModel
            {
                Role = AgencyMemberRole.Manager,
                Status = AgencyMemberStatus.Active
            },
            _ => new AgencyMemberAccessReadModel
            {
                Role = AgencyMemberRole.Owner,
                Status = AgencyMemberStatus.Active
            }
        };

        var userRepository = new FakeUserRepository(calls, user);
        var agencyRepository = new FakeAgencyRepository(
            calls,
            agency,
            member);
        var listingRepository = new FakeListingRepository(calls);
        var handler = new GetAgencyDashboardListingsHandler(
            new AgencyListingAccessChecker(agencyRepository),
            new GetAgencyDashboardListingsValidator(),
            listingRepository,
            userRepository,
            currentUser);

        return new HandlerFixture(handler, listingRepository, calls);
    }

    private static User CreateUser(UserStatus status)
    {
        return new User(
            $"user-{Guid.NewGuid():N}@example.com",
            "password-hash",
            "Test",
            "User",
            null,
            status: status);
    }

    private static Agency CreateAgency()
    {
        return new Agency(
            "Test Agency",
            $"test-agency-{Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static string[] ExpectedCalls(string scenario)
    {
        return scenario switch
        {
            "unauthenticated" => [],
            "missing-account" or "disabled-account" => ["user.get"],
            "missing-agency" => ["user.get", "agency.get"],
            "inactive-member" or "manager-role" =>
            ["user.get", "agency.get", "agency.member.get"],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }

    private static InvalidOperationException UnexpectedCall(
        string memberName)
    {
        return new InvalidOperationException(
            $"Unexpected repository call: {memberName}.");
    }

    private sealed record HandlerFixture(
        GetAgencyDashboardListingsHandler Handler,
        FakeListingRepository ListingRepository,
        List<string> Calls);

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; init; }

        public bool IsAuthenticated { get; init; }
    }

    private sealed class FakeUserRepository(
        List<string> calls,
        User? user) : IUserRepository
    {
        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            calls.Add("user.get");
            return Task.FromResult(user);
        }

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(ExistsByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByNormalizedEmailAsync));
        public Task<User?> GetByNormalizedEmailReadOnlyAsync(string normalizedEmail, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByNormalizedEmailReadOnlyAsync));
        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task AddAsync(User addedUser, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(AddAsync));
        public Task<UserRegistrationPersistenceResult> PersistRegistrationAsync(User addedUser, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(PersistRegistrationAsync));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SaveChangesAsync));
    }

    private sealed class FakeAgencyRepository(
        List<string> calls,
        Agency? agency,
        AgencyMemberAccessReadModel? member) : IAgencyRepository
    {
        public Task<Agency?> GetByIdReadOnlyAsync(
            Guid agencyId,
            CancellationToken cancellationToken)
        {
            calls.Add("agency.get");
            return Task.FromResult(agency);
        }

        public Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(
            Guid agencyId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            calls.Add("agency.member.get");
            return Task.FromResult(member);
        }

        public Task<AgencyCreationPersistenceResult> CreateAsync(Agency createdAgency, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(CreateAsync));
        public Task<Agency?> GetBySlugReadOnlyAsync(string slug, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetBySlugReadOnlyAsync));
        public Task<Agency?> GetByIdForUpdateAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task<Agency?> GetByIdWithMembersForUpdateAsync(Guid agencyId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdWithMembersForUpdateAsync));
        public void AddMember(AgencyMember addedMember) => throw UnexpectedCall(nameof(AddMember));
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

    private sealed class FakeListingRepository(List<string> calls)
        : IListingRepository
    {
        public int CallCount { get; private set; }

        public ListingStatus? Status { get; private set; }

        public Task<PagedResult<Listing>>
            GetByAgencyIdForDashboardReadOnlyAsync(
                Guid agencyId,
                ListingStatus? status,
                int page,
                int pageSize,
                CancellationToken cancellationToken)
        {
            calls.Add("listing.dashboard.get");
            CallCount++;
            Status = status;
            return Task.FromResult(
                new PagedResult<Listing>([], page, pageSize, 0));
        }

        public Task CreateAsync(Listing listing, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(CreateAsync));
        public Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(GetListingsQuery query, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetFilteredReadOnlyAsync));
        public Task<ComparableListingsReadResult> GetComparableListingsReadOnlyAsync(Guid sourceListingId, string languageCode, int limit, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetComparableListingsReadOnlyAsync));
        public Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdReadOnlyAsync));
        public Task<Listing?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdForUpdateAsync));
        public Task<ListingImageUploadProbeReadModel?> GetListingImageUploadProbeReadOnlyAsync(Guid listingId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetListingImageUploadProbeReadOnlyAsync));
        public Task<IListingImageWriteScope?> BeginListingImageWriteAsync(Guid listingId, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(BeginListingImageWriteAsync));
        public Task<Listing?> GetByIdWithImagesForUpdateAsync(Guid id, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByIdWithImagesForUpdateAsync));
        public Task<PagedResult<Listing>> GetByCreatedByUserIdAsync(Guid createdByUserId, int page, int pageSize, CancellationToken cancellationToken) => throw UnexpectedCall(nameof(GetByCreatedByUserIdAsync));
        public void AddListingImage(ListingImage image) => throw UnexpectedCall(nameof(AddListingImage));
        public void RemoveListingImage(ListingImage image) => throw UnexpectedCall(nameof(RemoveListingImage));
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw UnexpectedCall(nameof(SaveChangesAsync));
    }
}
