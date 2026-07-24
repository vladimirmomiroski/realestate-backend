using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Application.Agencies.Repositories;

namespace RealEstate.Tests.Integration.Agencies;

public sealed class AgencyInvitationPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public AgencyInvitationPersistenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task Can_save_and_load_agency_invitation()
    {
        // Arrange
        AuthenticatedTestUser inviter =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid invitationId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var agency = AgencyTestHelpers.CreateAgency();

            dbContext.Agencies.Add(agency);

            await dbContext.SaveChangesAsync();

            var invitation = CreateInvitation(
                agencyId: agency.Id,
                invitedByUserId: inviter.UserId,
                token: Guid.NewGuid().ToString("N"),
                email: "agent@test.com",
                normalizedEmail: "AGENT@TEST.COM");

            dbContext.AgencyInvitations.Add(invitation);

            await dbContext.SaveChangesAsync();

            invitationId = invitation.Id;
        }

        // Act
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var savedInvitation = await dbContext.AgencyInvitations
                .SingleAsync(invitation => invitation.Id == invitationId);

            // Assert
            savedInvitation.Email.Should().Be("agent@test.com");
            savedInvitation.NormalizedEmail.Should().Be("AGENT@TEST.COM");
            savedInvitation.Token.Should().NotBeNullOrWhiteSpace();
            savedInvitation.Code.Should().Be("123456");
            savedInvitation.Role.Should().Be(AgencyMemberRole.Agent);
            savedInvitation.Status.Should().Be(AgencyInvitationStatus.Pending);
            savedInvitation.CreatedAtUtc.Should().NotBe(default);
            savedInvitation.AgencyId.Should().NotBeEmpty();
            savedInvitation.InvitedByUserId.Should().Be(inviter.UserId);
        }
    }

    [Fact]
    public async Task Token_must_be_unique()
    {
        // Arrange
        AuthenticatedTestUser inviter =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var agency = AgencyTestHelpers.CreateAgency();

        dbContext.Agencies.Add(agency);

        await dbContext.SaveChangesAsync();

        string token = Guid.NewGuid().ToString("N");

        var firstInvitation = CreateInvitation(
            agencyId: agency.Id,
            invitedByUserId: inviter.UserId,
            token: token,
            email: "first@test.com",
            normalizedEmail: "FIRST@TEST.COM");

        var secondInvitation = CreateInvitation(
            agencyId: agency.Id,
            invitedByUserId: inviter.UserId,
            token: token,
            email: "second@test.com",
            normalizedEmail: "SECOND@TEST.COM");

        dbContext.AgencyInvitations.Add(firstInvitation);

        await dbContext.SaveChangesAsync();

        dbContext.AgencyInvitations.Add(secondInvitation);

        // Act
        var act = async () => await dbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PersistAcceptanceAsync_ShouldPropagateUnrelatedDatabaseError_AndRollback()
    {
        // Arrange
        AuthenticatedTestUser inviter =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId;
        Guid invitationId;
        string token;

        using (IServiceScope seedScope =
               _factory.Services.CreateScope())
        {
            var seedDbContext =
                seedScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            var agency = AgencyTestHelpers.CreateAgency();

            seedDbContext.Agencies.Add(agency);

            await seedDbContext.SaveChangesAsync();

            var invitation = CreateInvitation(
                agencyId: agency.Id,
                invitedByUserId: inviter.UserId,
                token: Guid.NewGuid().ToString("N"),
                email: "foreign-key-test@test.com",
                normalizedEmail:
                    "FOREIGN-KEY-TEST@TEST.COM");

            seedDbContext.AgencyInvitations.Add(
                invitation);

            await seedDbContext.SaveChangesAsync();

            agencyId = agency.Id;
            invitationId = invitation.Id;
            token = invitation.Token;
        }

        Guid nonexistentUserId = Guid.NewGuid();

        using (IServiceScope mutationServiceScope =
               _factory.Services.CreateScope())
        {
            var mutationDbContext =
                mutationServiceScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            var invitationRepository =
                mutationServiceScope.ServiceProvider
                    .GetRequiredService<
                        IAgencyInvitationRepository>();

            IAgencyInvitationTerminalMutationScope?
                terminalMutationScope =
                    await invitationRepository
                        .BeginTerminalMutationByTokenAsync(
                            token,
                            CancellationToken.None);

            terminalMutationScope.Should().NotBeNull();

            await using (terminalMutationScope!)
            {
                terminalMutationScope.Invitation.Accept(
                    inviter.UserId,
                    DateTime.UtcNow);

                var invalidMember = new AgencyMember(
                    agencyId,
                    nonexistentUserId,
                    AgencyMemberRole.Agent,
                    AgencyMemberStatus.Active);

                mutationDbContext
                    .Set<AgencyMember>()
                    .Add(invalidMember);

                // Act
                Func<Task> act = async () =>
                    await terminalMutationScope
                        .PersistAcceptanceAsync(
                            CancellationToken.None);

                // Assert
                await act.Should()
                    .ThrowAsync<DbUpdateException>();
            }
        }

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var assertionDbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        AgencyInvitation savedInvitation =
            await assertionDbContext.AgencyInvitations
                .AsNoTracking()
                .SingleAsync(invitation =>
                    invitation.Id == invitationId);

        int invalidMembershipCount =
            await assertionDbContext
                .Set<AgencyMember>()
                .AsNoTracking()
                .CountAsync(member =>
                    member.AgencyId == agencyId &&
                    member.UserId == nonexistentUserId);

        savedInvitation.Status.Should()
            .Be(AgencyInvitationStatus.Pending);

        savedInvitation.AcceptedByUserId.Should()
            .BeNull();

        savedInvitation.AcceptedAtUtc.Should()
            .BeNull();

        invalidMembershipCount.Should().Be(0);
    }

    [Fact]
    public async Task PersistNewInvitationAsync_ShouldPropagateTokenConflict_AndRollbackObservedExpiry()
    {
        // Arrange
        AuthenticatedTestUser inviter =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId;
        Guid elapsedInvitationId;

        string invitedEmail =
            $"replacement-{Guid.NewGuid():N}@test.com";

        string normalizedEmail =
            invitedEmail.ToUpperInvariant();

        string duplicateToken =
            Guid.NewGuid().ToString("N");

        using (IServiceScope seedScope =
               _factory.Services.CreateScope())
        {
            var seedDbContext =
                seedScope.ServiceProvider
                    .GetRequiredService<
                        RealEstateDbContext>();

            var agency =
                AgencyTestHelpers.CreateAgency();

            seedDbContext.Agencies.Add(agency);

            await seedDbContext.SaveChangesAsync();

            var elapsedInvitation =
                new AgencyInvitation(
                    agencyId: agency.Id,
                    email: invitedEmail,
                    normalizedEmail:
                        normalizedEmail,
                    token:
                        Guid.NewGuid().ToString("N"),
                    code: "123456",
                    role: AgencyMemberRole.Agent,
                    invitedByUserId: inviter.UserId,
                    expiresAtUtc:
                        DateTime.UtcNow.AddDays(-1));

            var tokenCollisionInvitation =
                new AgencyInvitation(
                    agencyId: agency.Id,
                    email:
                        $"token-owner-{Guid.NewGuid():N}@test.com",
                    normalizedEmail:
                        $"TOKEN-OWNER-{Guid.NewGuid():N}@TEST.COM",
                    token: duplicateToken,
                    code: "654321",
                    role: AgencyMemberRole.Agent,
                    invitedByUserId: inviter.UserId,
                    expiresAtUtc:
                        DateTime.UtcNow.AddDays(7));

            tokenCollisionInvitation.Cancel(
                DateTime.UtcNow);

            seedDbContext.AgencyInvitations.AddRange(
                elapsedInvitation,
                tokenCollisionInvitation);

            await seedDbContext.SaveChangesAsync();

            agencyId = agency.Id;
            elapsedInvitationId =
                elapsedInvitation.Id;
        }

        Guid attemptedReplacementId =
            Guid.Empty;

        using (IServiceScope mutationScope =
               _factory.Services.CreateScope())
        {
            var repository =
                mutationScope.ServiceProvider
                    .GetRequiredService<
                        IAgencyInvitationRepository>();

            IAgencyInvitationCreationScope
                creationScope =
                    await repository
                        .BeginCreateOrReplaceAsync(
                            agencyId,
                            normalizedEmail,
                            CancellationToken.None);

            await using (creationScope)
            {
                creationScope.PendingInvitation
                    .Should()
                    .NotBeNull();

                creationScope.PendingInvitation!.Id
                    .Should()
                    .Be(elapsedInvitationId);

                creationScope.PendingInvitation
                    .MarkExpired(DateTime.UtcNow);

                await creationScope
                    .PersistObservedExpiryAsync(
                        CancellationToken.None);

                var replacement =
                    new AgencyInvitation(
                        agencyId: agencyId,
                        email: invitedEmail,
                        normalizedEmail:
                            normalizedEmail,
                        token: duplicateToken,
                        code: "111111",
                        role: AgencyMemberRole.Agent,
                        invitedByUserId:
                            inviter.UserId,
                        expiresAtUtc:
                            DateTime.UtcNow.AddDays(7));

                attemptedReplacementId =
                    replacement.Id;

                // Act
                Func<Task> act = async () =>
                    await creationScope
                        .PersistNewInvitationAsync(
                            replacement,
                            CancellationToken.None);

                // Assert
                await act.Should()
                    .ThrowAsync<DbUpdateException>();
            }
        }

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var assertionDbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<AgencyInvitation> matchingInvitations =
            await assertionDbContext
                .AgencyInvitations
                .AsNoTracking()
                .Where(invitation =>
                    invitation.AgencyId == agencyId &&
                    invitation.NormalizedEmail ==
                        normalizedEmail)
                .ToListAsync();

        matchingInvitations.Should()
            .ContainSingle();

        AgencyInvitation savedInvitation =
            matchingInvitations.Single();

        savedInvitation.Id.Should()
            .Be(elapsedInvitationId);

        savedInvitation.Status.Should()
            .Be(AgencyInvitationStatus.Pending);

        bool attemptedReplacementExists =
            await assertionDbContext
                .AgencyInvitations
                .AsNoTracking()
                .AnyAsync(invitation =>
                    invitation.Id ==
                    attemptedReplacementId);

        attemptedReplacementExists.Should()
            .BeFalse();
    }

    private static AgencyInvitation CreateInvitation(
        Guid agencyId,
        Guid invitedByUserId,
        string token,
        string email,
        string normalizedEmail)
    {
        return new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: normalizedEmail,
            token: token,
            code: "123456",
            role: AgencyMemberRole.Agent,
            invitedByUserId: invitedByUserId,
            expiresAtUtc: DateTime.UtcNow.AddDays(7));
    }
}
