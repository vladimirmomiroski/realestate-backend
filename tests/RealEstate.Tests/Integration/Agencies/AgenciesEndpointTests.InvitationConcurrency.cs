using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    private static readonly TimeSpan
        InvitationConcurrencyTimeout =
            TimeSpan.FromSeconds(20);

    [Fact]
    public async Task
        AgencyInvitationConcurrency_AcceptVersusAccept_ShouldHaveOneWinner()
    {
        // Arrange
        InvitationConcurrencySeed seed =
            await CreateInvitationConcurrencySeedAsync();

        using HttpClient firstClient =
            CreateInvitationConcurrencyClient(
                seed.InvitedUser.AccessToken);

        using HttpClient secondClient =
            CreateInvitationConcurrencyClient(
                seed.InvitedUser.AccessToken);

        // Act
        (
            HttpResponseMessage firstResponse,
            HttpResponseMessage secondResponse
        ) = await ExecuteMembershipGateRaceAsync(
            seed.AgencyId,
            seed.InvitedUser.UserId,
            firstRequest: cancellationToken =>
                firstClient.PutAsJsonAsync(
                    "/api/agencies/invitations/accept",
                    new { token = seed.Token },
                    cancellationToken),
            secondRequest: cancellationToken =>
                secondClient.PutAsJsonAsync(
                    "/api/agencies/invitations/accept",
                    new { token = seed.Token },
                    cancellationToken));

        using (firstResponse)
        using (secondResponse)
        {
            // Assert
            firstResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            await AssertResourceStateConflictAsync(
                secondResponse,
                "/api/agencies/invitations/accept");
        }

        InvitationCommittedState state =
            await ReadInvitationCommittedStateAsync(
                seed.InvitationId,
                seed.AgencyId,
                seed.InvitedUser.UserId);

        state.Status.Should()
            .Be(AgencyInvitationStatus.Accepted);

        state.AcceptedByUserId.Should()
            .Be(seed.InvitedUser.UserId);

        state.AcceptedAtUtc.Should().NotBeNull();
        state.CancelledAtUtc.Should().BeNull();

        state.Memberships.Should().ContainSingle();

        InvitationMembershipState membership =
            state.Memberships.Single();

        membership.Role.Should()
            .Be(AgencyMemberRole.Agent);

        membership.Status.Should()
            .Be(AgencyMemberStatus.Active);
    }

    [Fact]
    public async Task
        AgencyInvitationConcurrency_AcceptVersusCancel_ShouldKeepAcceptedStateAndMembership()
    {
        // Arrange
        InvitationConcurrencySeed seed =
            await CreateInvitationConcurrencySeedAsync();

        using HttpClient acceptClient =
            CreateInvitationConcurrencyClient(
                seed.InvitedUser.AccessToken);

        using HttpClient cancelClient =
            CreateInvitationConcurrencyClient(
                seed.Owner.AccessToken);

        // Act
        (
            HttpResponseMessage acceptResponse,
            HttpResponseMessage cancelResponse
        ) = await ExecuteMembershipGateRaceAsync(
            seed.AgencyId,
            seed.InvitedUser.UserId,
            firstRequest: cancellationToken =>
                acceptClient.PutAsJsonAsync(
                    "/api/agencies/invitations/accept",
                    new { token = seed.Token },
                    cancellationToken),
            secondRequest: cancellationToken =>
                cancelClient.PutAsync(
                    $"/api/agencies/{seed.AgencyId}" +
                    $"/invitations/{seed.InvitationId}/cancel",
                    content: null,
                    cancellationToken));

        using (acceptResponse)
        using (cancelResponse)
        {
            // Assert
            acceptResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            await AssertResourceStateConflictAsync(
                cancelResponse,
                $"/api/agencies/{seed.AgencyId}" +
                $"/invitations/{seed.InvitationId}/cancel");
        }

        InvitationCommittedState state =
            await ReadInvitationCommittedStateAsync(
                seed.InvitationId,
                seed.AgencyId,
                seed.InvitedUser.UserId);

        state.Status.Should()
            .Be(AgencyInvitationStatus.Accepted);

        state.AcceptedByUserId.Should()
            .Be(seed.InvitedUser.UserId);

        state.AcceptedAtUtc.Should().NotBeNull();
        state.CancelledAtUtc.Should().BeNull();

        state.Memberships.Should().ContainSingle();

        InvitationMembershipState membership =
            state.Memberships.Single();

        membership.Role.Should()
            .Be(AgencyMemberRole.Agent);

        membership.Status.Should()
            .Be(AgencyMemberStatus.Active);
    }

    [Fact]
    public async Task
        AgencyInvitationConcurrency_ExpiryObservationVersusCancel_ShouldKeepExpiredState()
    {
        // Arrange
        InvitationConcurrencySeed seed =
            await CreateInvitationConcurrencySeedAsync(
                expiresAtUtc: DateTime.UtcNow.AddDays(-1));

        using HttpClient acceptClient =
            CreateInvitationConcurrencyClient(
                seed.InvitedUser.AccessToken);

        using HttpClient cancelClient =
            CreateInvitationConcurrencyClient(
                seed.Owner.AccessToken);

        // Act
        (
            HttpResponseMessage acceptResponse,
            HttpResponseMessage cancelResponse
        ) = await ExecuteInvitationLockQueueRaceAsync(
            seed.InvitationId,
            firstRequest: cancellationToken =>
                acceptClient.PutAsJsonAsync(
                    "/api/agencies/invitations/accept",
                    new { token = seed.Token },
                    cancellationToken),
            secondRequest: cancellationToken =>
                cancelClient.PutAsync(
                    $"/api/agencies/{seed.AgencyId}" +
                    $"/invitations/{seed.InvitationId}/cancel",
                    content: null,
                    cancellationToken));

        using (acceptResponse)
        using (cancelResponse)
        {
            // Assert
            await AssertResourceStateConflictAsync(
                acceptResponse,
                "/api/agencies/invitations/accept");

            await AssertResourceStateConflictAsync(
                cancelResponse,
                $"/api/agencies/{seed.AgencyId}" +
                $"/invitations/{seed.InvitationId}/cancel");
        }

        InvitationCommittedState state =
            await ReadInvitationCommittedStateAsync(
                seed.InvitationId,
                seed.AgencyId,
                seed.InvitedUser.UserId);

        state.Status.Should()
            .Be(AgencyInvitationStatus.Expired);

        state.AcceptedByUserId.Should().BeNull();
        state.AcceptedAtUtc.Should().BeNull();
        state.CancelledAtUtc.Should().BeNull();

        state.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task
        AgencyInvitationConcurrency_MembershipConflict_ShouldRollbackAcceptance()
    {
        // Arrange
        InvitationConcurrencySeed seed =
            await CreateInvitationConcurrencySeedAsync();

        using HttpClient acceptClient =
            CreateInvitationConcurrencyClient(
                seed.InvitedUser.AccessToken);

        // Act
        (
            HttpResponseMessage response,
            Guid committedGateMemberId
        ) = await ExecuteForcedMembershipConflictAsync(
            seed.AgencyId,
            seed.InvitedUser.UserId,
            cancellationToken =>
                acceptClient.PutAsJsonAsync(
                    "/api/agencies/invitations/accept",
                    new { token = seed.Token },
                    cancellationToken));

        using (response)
        {
            // Assert
            await AssertResourceStateConflictAsync(
                response,
                "/api/agencies/invitations/accept");
        }

        InvitationCommittedState state =
            await ReadInvitationCommittedStateAsync(
                seed.InvitationId,
                seed.AgencyId,
                seed.InvitedUser.UserId);

        state.Status.Should()
            .Be(AgencyInvitationStatus.Pending);

        state.AcceptedByUserId.Should().BeNull();
        state.AcceptedAtUtc.Should().BeNull();
        state.CancelledAtUtc.Should().BeNull();

        state.Memberships.Should().ContainSingle();

        InvitationMembershipState membership =
            state.Memberships.Single();

        membership.Id.Should()
            .Be(committedGateMemberId);

        membership.Role.Should()
            .Be(AgencyMemberRole.Agent);

        membership.Status.Should()
            .Be(AgencyMemberStatus.Active);
    }

    [Fact]
    public async Task
    AgencyInvitationConcurrency_AcceptVersusCreate_ShouldNotCreatePendingInvitationForNewMember()
    {
        // Arrange
        InvitationConcurrencySeed seed =
            await CreateInvitationConcurrencySeedAsync();

        using HttpClient acceptClient =
            CreateInvitationConcurrencyClient(
                seed.InvitedUser.AccessToken);

        using HttpClient createClient =
            CreateInvitationConcurrencyClient(
                seed.Owner.AccessToken);

        // Act
        (
            HttpResponseMessage acceptResponse,
            HttpResponseMessage createResponse
        ) = await ExecuteMembershipGateRaceAsync(
            seed.AgencyId,
            seed.InvitedUser.UserId,
            firstRequest: cancellationToken =>
                acceptClient.PutAsJsonAsync(
                    "/api/agencies/invitations/accept",
                    new { token = seed.Token },
                    cancellationToken),
            secondRequest: cancellationToken =>
                createClient.PostAsJsonAsync(
                    $"/api/agencies/{seed.AgencyId}" +
                    "/invitations",
                    CreateValidCreateAgencyInvitationRequest(
                        seed.InvitedUser.Email),
                    cancellationToken));

        using (acceptResponse)
        using (createResponse)
        {
            // Assert
            acceptResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            await AssertResourceStateConflictAsync(
                createResponse,
                $"/api/agencies/{seed.AgencyId}/invitations");
        }

        InvitationCommittedState state =
            await ReadInvitationCommittedStateAsync(
                seed.InvitationId,
                seed.AgencyId,
                seed.InvitedUser.UserId);

        state.Status.Should()
            .Be(AgencyInvitationStatus.Accepted);

        state.AcceptedByUserId.Should()
            .Be(seed.InvitedUser.UserId);

        state.Memberships.Should()
            .ContainSingle();

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<AgencyInvitation> matchingInvitations =
            await dbContext.AgencyInvitations
                .AsNoTracking()
                .Where(invitation =>
                    invitation.AgencyId ==
                        seed.AgencyId &&
                    invitation.NormalizedEmail ==
                        seed.InvitedUser.Email
                            .ToUpperInvariant())
                .ToListAsync();

        matchingInvitations.Should()
            .ContainSingle();

        matchingInvitations.Single().Id.Should()
            .Be(seed.InvitationId);

        matchingInvitations.Single().Status.Should()
            .Be(AgencyInvitationStatus.Accepted);

        matchingInvitations.Should()
            .NotContain(invitation =>
                invitation.Status ==
                AgencyInvitationStatus.Pending);
    }

    [Fact]
    public async Task
    AgencyInvitationConcurrency_CreateVersusCreate_ShouldHaveOnePendingWinner()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyAsAsync(owner);

        string invitedEmail =
            $"concurrent-create-{Guid.NewGuid():N}@test.com";

        string normalizedEmail =
            invitedEmail.ToUpperInvariant();

        using HttpClient firstClient =
            CreateInvitationConcurrencyClient(
                owner.AccessToken);

        using HttpClient secondClient =
            CreateInvitationConcurrencyClient(
                owner.AccessToken);

        // Act
        (
            HttpResponseMessage firstResponse,
            HttpResponseMessage secondResponse,
            Guid gateInvitationId
        ) = await ExecuteConcurrentCreateGateRaceAsync(
            agencyId,
            owner.UserId,
            invitedEmail,
            firstRequest: cancellationToken =>
                firstClient.PostAsJsonAsync(
                    $"/api/agencies/{agencyId}/invitations",
                    CreateValidCreateAgencyInvitationRequest(
                        invitedEmail),
                    cancellationToken),
            secondRequest: cancellationToken =>
                secondClient.PostAsJsonAsync(
                    $"/api/agencies/{agencyId}/invitations",
                    CreateValidCreateAgencyInvitationRequest(
                        invitedEmail),
                    cancellationToken));

        Guid winnerInvitationId;

        using (firstResponse)
        using (secondResponse)
        {
            // Assert
            new[]
            {
            firstResponse.StatusCode,
            secondResponse.StatusCode
        }.Should().BeEquivalentTo(
                new[]
                {
                HttpStatusCode.Created,
                HttpStatusCode.Conflict
                });

            HttpResponseMessage winnerResponse =
                firstResponse.StatusCode ==
                HttpStatusCode.Created
                    ? firstResponse
                    : secondResponse;

            HttpResponseMessage loserResponse =
                firstResponse.StatusCode ==
                HttpStatusCode.Conflict
                    ? firstResponse
                    : secondResponse;

            JsonElement winnerJson =
                await winnerResponse.Content
                    .ReadFromJsonAsync<JsonElement>();

            winnerInvitationId =
                winnerJson.GetProperty("id").GetGuid();

            await AssertResourceStateConflictAsync(
                loserResponse,
                $"/api/agencies/{agencyId}/invitations");
        }

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<AgencyInvitation> matchingInvitations =
            await dbContext.AgencyInvitations
                .AsNoTracking()
                .Where(invitation =>
                    invitation.AgencyId == agencyId &&
                    invitation.NormalizedEmail ==
                        normalizedEmail)
                .ToListAsync();

        matchingInvitations.Should()
            .ContainSingle();

        AgencyInvitation committedInvitation =
            matchingInvitations.Single();

        committedInvitation.Id.Should()
            .Be(winnerInvitationId);

        committedInvitation.Id.Should()
            .NotBe(gateInvitationId);

        committedInvitation.Status.Should()
            .Be(AgencyInvitationStatus.Pending);
    }

    private async Task<InvitationConcurrencySeed>
        CreateInvitationConcurrencySeedAsync(
            DateTime? expiresAtUtc = null)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        AuthenticatedTestUser invitedUser =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyAsAsync(owner);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: invitedUser.Email,
            normalizedEmail:
                invitedUser.Email.ToUpperInvariant(),
            token: Guid.NewGuid().ToString("N"),
            code: Random.Shared
                .Next(0, 1_000_000)
                .ToString("D6"),
            role: AgencyMemberRole.Agent,
            invitedByUserId: owner.UserId,
            expiresAtUtc:
                expiresAtUtc ??
                DateTime.UtcNow.AddDays(7));

        dbContext.AgencyInvitations.Add(invitation);

        await dbContext.SaveChangesAsync();

        return new InvitationConcurrencySeed(
            owner,
            invitedUser,
            agencyId,
            invitation.Id,
            invitation.Token);
    }

    private HttpClient
        CreateInvitationConcurrencyClient(
            string accessToken)
    {
        HttpClient client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        return client;
    }

    private async Task<(
        HttpResponseMessage FirstResponse,
        HttpResponseMessage SecondResponse)>
        ExecuteMembershipGateRaceAsync(
            Guid agencyId,
            Guid invitedUserId,
            Func<CancellationToken,
                Task<HttpResponseMessage>> firstRequest,
            Func<CancellationToken,
                Task<HttpResponseMessage>> secondRequest)
    {
        using var timeout =
            new CancellationTokenSource(
                InvitationConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeout.Token;

        using IServiceScope gateScope =
            _factory.Services.CreateScope();

        using IServiceScope observerScope =
            _factory.Services.CreateScope();

        var gateDbContext =
            gateScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        var observerDbContext =
            observerScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        await using IDbContextTransaction gateTransaction =
            await gateDbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

        var gateMember = new AgencyMember(
            agencyId,
            invitedUserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);

        gateDbContext.Set<AgencyMember>()
            .Add(gateMember);

        await gateDbContext.SaveChangesAsync(
            cancellationToken);

        int gateBackendPid =
            await GetBackendPidAsync(
                gateDbContext,
                cancellationToken);

        Task<HttpResponseMessage>? firstTask = null;
        Task<HttpResponseMessage>? secondTask = null;

        bool gateReleased = false;
        bool responsesCompleted = false;

        try
        {
            firstTask = firstRequest(cancellationToken);

            int firstRequestBackendPid =
                await WaitForBlockedBackendAsync(
                    observerDbContext,
                    blockingBackendPid:
                        gateBackendPid,
                    queryPattern:
                        "%INSERT INTO \"AgencyMembers\"%",
                    requireForUpdate: false,
                    cancellationToken);

            secondTask = secondRequest(
                cancellationToken);

            await WaitForBlockedBackendAsync(
                observerDbContext,
                blockingBackendPid:
                    firstRequestBackendPid,
                queryPattern:
                    "%FROM \"AgencyInvitations\"%",
                requireForUpdate: true,
                cancellationToken);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            HttpResponseMessage firstResponse =
                await firstTask.WaitAsync(
                    cancellationToken);

            HttpResponseMessage secondResponse =
                await secondTask.WaitAsync(
                    cancellationToken);

            responsesCompleted = true;

            return (
                firstResponse,
                secondResponse);
        }
        finally
        {
            if (!gateReleased)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (!responsesCompleted)
            {
                timeout.Cancel();

                await DrainAndDisposeResponseTaskAsync(
                    firstTask);

                await DrainAndDisposeResponseTaskAsync(
                    secondTask);
            }
        }
    }

    private async Task<(
        HttpResponseMessage FirstResponse,
        HttpResponseMessage SecondResponse)>
        ExecuteInvitationLockQueueRaceAsync(
            Guid invitationId,
            Func<CancellationToken,
                Task<HttpResponseMessage>> firstRequest,
            Func<CancellationToken,
                Task<HttpResponseMessage>> secondRequest)
    {
        using var timeout =
            new CancellationTokenSource(
                InvitationConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeout.Token;

        using IServiceScope gateScope =
            _factory.Services.CreateScope();

        using IServiceScope observerScope =
            _factory.Services.CreateScope();

        var gateDbContext =
            gateScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        var observerDbContext =
            observerScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        await using IDbContextTransaction gateTransaction =
            await gateDbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

        int gateBackendPid =
            await GetBackendPidAsync(
                gateDbContext,
                cancellationToken);

        await LockInvitationAsync(
            gateDbContext,
            invitationId,
            cancellationToken);

        Task<HttpResponseMessage>? firstTask = null;
        Task<HttpResponseMessage>? secondTask = null;

        bool gateReleased = false;
        bool responsesCompleted = false;

        try
        {
            firstTask = firstRequest(cancellationToken);

            int firstRequestBackendPid =
                await WaitForBlockedBackendAsync(
                    observerDbContext,
                    blockingBackendPid:
                        gateBackendPid,
                    queryPattern:
                        "%FROM \"AgencyInvitations\"%",
                    requireForUpdate: true,
                    cancellationToken);

            secondTask = secondRequest(
                cancellationToken);

            await WaitForBlockedBackendAsync(
                observerDbContext,
                blockingBackendPid:
                    firstRequestBackendPid,
                queryPattern:
                    "%FROM \"AgencyInvitations\"%",
                requireForUpdate: true,
                cancellationToken);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            HttpResponseMessage firstResponse =
                await firstTask.WaitAsync(
                    cancellationToken);

            HttpResponseMessage secondResponse =
                await secondTask.WaitAsync(
                    cancellationToken);

            responsesCompleted = true;

            return (
                firstResponse,
                secondResponse);
        }
        finally
        {
            if (!gateReleased)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (!responsesCompleted)
            {
                timeout.Cancel();

                await DrainAndDisposeResponseTaskAsync(
                    firstTask);

                await DrainAndDisposeResponseTaskAsync(
                    secondTask);
            }
        }
    }

    private async Task<(
        HttpResponseMessage Response,
        Guid CommittedGateMemberId)>
        ExecuteForcedMembershipConflictAsync(
            Guid agencyId,
            Guid invitedUserId,
            Func<CancellationToken,
                Task<HttpResponseMessage>> request)
    {
        using var timeout =
            new CancellationTokenSource(
                InvitationConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeout.Token;

        using IServiceScope gateScope =
            _factory.Services.CreateScope();

        using IServiceScope observerScope =
            _factory.Services.CreateScope();

        var gateDbContext =
            gateScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        var observerDbContext =
            observerScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        await using IDbContextTransaction gateTransaction =
            await gateDbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

        var gateMember = new AgencyMember(
            agencyId,
            invitedUserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);

        gateDbContext.Set<AgencyMember>()
            .Add(gateMember);

        await gateDbContext.SaveChangesAsync(
            cancellationToken);

        int gateBackendPid =
            await GetBackendPidAsync(
                gateDbContext,
                cancellationToken);

        Task<HttpResponseMessage>? requestTask = null;

        bool gateCompleted = false;
        bool responseCompleted = false;

        try
        {
            requestTask = request(cancellationToken);

            await WaitForBlockedBackendAsync(
                observerDbContext,
                blockingBackendPid:
                    gateBackendPid,
                queryPattern:
                    "%INSERT INTO \"AgencyMembers\"%",
                requireForUpdate: false,
                cancellationToken);

            await gateTransaction.CommitAsync(
                CancellationToken.None);

            gateCompleted = true;

            HttpResponseMessage response =
                await requestTask.WaitAsync(
                    cancellationToken);

            responseCompleted = true;

            return (
                response,
                gateMember.Id);
        }
        finally
        {
            if (!gateCompleted)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (!responseCompleted)
            {
                timeout.Cancel();

                await DrainAndDisposeResponseTaskAsync(
                    requestTask);
            }
        }
    }

    private async Task<(
    HttpResponseMessage FirstResponse,
    HttpResponseMessage SecondResponse,
    Guid GateInvitationId)>
    ExecuteConcurrentCreateGateRaceAsync(
        Guid agencyId,
        Guid invitedByUserId,
        string email,
        Func<CancellationToken,
            Task<HttpResponseMessage>> firstRequest,
        Func<CancellationToken,
            Task<HttpResponseMessage>> secondRequest)
    {
        using var timeout =
            new CancellationTokenSource(
                InvitationConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeout.Token;

        using IServiceScope gateScope =
            _factory.Services.CreateScope();

        using IServiceScope observerScope =
            _factory.Services.CreateScope();

        var gateDbContext =
            gateScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        var observerDbContext =
            observerScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        await using IDbContextTransaction
            gateTransaction =
                await gateDbContext.Database
                    .BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

        var gateInvitation =
            new AgencyInvitation(
                agencyId: agencyId,
                email: email,
                normalizedEmail:
                    email.ToUpperInvariant(),
                token: Guid.NewGuid().ToString("N"),
                code: "123456",
                role: AgencyMemberRole.Agent,
                invitedByUserId: invitedByUserId,
                expiresAtUtc:
                    DateTime.UtcNow.AddDays(7));

        gateDbContext.AgencyInvitations.Add(
            gateInvitation);

        await gateDbContext.SaveChangesAsync(
            cancellationToken);

        int gateBackendPid =
            await GetBackendPidAsync(
                gateDbContext,
                cancellationToken);

        Task<HttpResponseMessage>? firstTask = null;
        Task<HttpResponseMessage>? secondTask = null;

        bool gateReleased = false;
        bool responsesCompleted = false;

        try
        {
            firstTask = firstRequest(
                cancellationToken);

            int firstRequestBackendPid =
                await WaitForBlockedBackendAsync(
                    observerDbContext,
                    blockingBackendPid:
                        gateBackendPid,
                    queryPattern:
                        "%INSERT INTO \"AgencyInvitations\"%",
                    requireForUpdate: false,
                    cancellationToken);

            secondTask = secondRequest(
                cancellationToken);

            await WaitForBlockedBackendAsync(
                observerDbContext,
                blockingBackendPid:
                    gateBackendPid,
                queryPattern:
                    "%INSERT INTO \"AgencyInvitations\"%",
                requireForUpdate: false,
                cancellationToken,
                excludedBackendPid:
                    firstRequestBackendPid);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            HttpResponseMessage firstResponse =
                await firstTask.WaitAsync(
                    cancellationToken);

            HttpResponseMessage secondResponse =
                await secondTask.WaitAsync(
                    cancellationToken);

            responsesCompleted = true;

            return (
                firstResponse,
                secondResponse,
                gateInvitation.Id);
        }
        finally
        {
            if (!gateReleased)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve original orchestration failure.
                }
            }

            if (!responsesCompleted)
            {
                timeout.Cancel();

                await DrainAndDisposeResponseTaskAsync(
                    firstTask);

                await DrainAndDisposeResponseTaskAsync(
                    secondTask);
            }
        }
    }

    private static async Task<int>
        GetBackendPidAsync(
            RealEstateDbContext dbContext,
            CancellationToken cancellationToken)
    {
        await EnsureConnectionOpenAsync(
            dbContext,
            cancellationToken);

        DbConnection connection =
            dbContext.Database.GetDbConnection();

        await using DbCommand command =
            connection.CreateCommand();

        command.Transaction =
            dbContext.Database.CurrentTransaction?
                .GetDbTransaction();

        command.CommandText =
            "SELECT pg_backend_pid();";

        command.CommandTimeout = 2;

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt32(result);
    }

    private static async Task
        LockInvitationAsync(
            RealEstateDbContext dbContext,
            Guid invitationId,
            CancellationToken cancellationToken)
    {
        await EnsureConnectionOpenAsync(
            dbContext,
            cancellationToken);

        DbConnection connection =
            dbContext.Database.GetDbConnection();

        await using DbCommand command =
            connection.CreateCommand();

        command.Transaction =
            dbContext.Database.CurrentTransaction?
                .GetDbTransaction();

        command.CommandText =
            """
            SELECT "Id"
            FROM "AgencyInvitations"
            WHERE "Id" = @invitationId
            FOR UPDATE;
            """;

        command.CommandTimeout = 2;

        AddParameter(
            command,
            "invitationId",
            DbType.Guid,
            invitationId);

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        result.Should().NotBeNull();
        ((Guid)result!).Should().Be(invitationId);
    }

    private static async Task<int>
    WaitForBlockedBackendAsync(
        RealEstateDbContext observerDbContext,
        int blockingBackendPid,
        string queryPattern,
        bool requireForUpdate,
        CancellationToken cancellationToken,
        int? excludedBackendPid = null)
    {
        await EnsureConnectionOpenAsync(
            observerDbContext,
            cancellationToken);

        DbConnection connection =
            observerDbContext.Database
                .GetDbConnection();

        while (true)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await using DbCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT activity.pid
                FROM pg_stat_activity AS activity
                WHERE activity.datname = current_database()
                    AND activity.pid <> pg_backend_pid()
                    AND (
                         @excludedBackendPid IS NULL
                         OR activity.pid <> @excludedBackendPid
                        )
                    AND activity.wait_event_type = 'Lock'
                    AND activity.query ILIKE @queryPattern
                    AND (
                         @requireForUpdate = FALSE
                         OR activity.query ILIKE '%FOR UPDATE%'
                        )
                    AND @blockingBackendPid =
                        ANY(pg_blocking_pids(activity.pid))
                ORDER BY activity.query_start
                LIMIT 1;
                """;

            command.CommandTimeout = 2;

            object excludedBackendPidValue =
                excludedBackendPid.HasValue
                    ? excludedBackendPid.Value
                    : DBNull.Value;

            AddParameter(
                command,
                "excludedBackendPid",
                DbType.Int32,
                excludedBackendPidValue);

            AddParameter(
                command,
                "queryPattern",
                DbType.String,
                queryPattern);

            AddParameter(
                command,
                "requireForUpdate",
                DbType.Boolean,
                requireForUpdate);

            AddParameter(
                command,
                "blockingBackendPid",
                DbType.Int32,
                blockingBackendPid);

            object? result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (result is not null &&
                result is not DBNull)
            {
                return Convert.ToInt32(result);
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                cancellationToken);
        }
    }

    private static async Task
        EnsureConnectionOpenAsync(
            RealEstateDbContext dbContext,
            CancellationToken cancellationToken)
    {
        if (dbContext.Database
                .GetDbConnection()
                .State != ConnectionState.Open)
        {
            await dbContext.Database
                .OpenConnectionAsync(
                    cancellationToken);
        }
    }

    private static async Task
        DrainAndDisposeResponseTaskAsync(
            Task<HttpResponseMessage>? responseTask)
    {
        if (responseTask is null)
        {
            return;
        }

        try
        {
            HttpResponseMessage response =
                await responseTask.WaitAsync(
                    TimeSpan.FromSeconds(5));

            response.Dispose();
        }
        catch
        {
            // Failure-path cleanup must not replace
            // the original orchestration failure.
        }
    }

    private async Task<InvitationCommittedState>
        ReadInvitationCommittedStateAsync(
            Guid invitationId,
            Guid agencyId,
            Guid invitedUserId)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        AgencyInvitation invitation =
            await dbContext.AgencyInvitations
                .AsNoTracking()
                .SingleAsync(current =>
                    current.Id == invitationId);

        List<InvitationMembershipState> memberships =
            await dbContext.Set<AgencyMember>()
                .AsNoTracking()
                .Where(member =>
                    member.AgencyId == agencyId &&
                    member.UserId == invitedUserId)
                .Select(member =>
                    new InvitationMembershipState(
                        member.Id,
                        member.Role,
                        member.Status))
                .ToListAsync();

        return new InvitationCommittedState(
            invitation.Status,
            invitation.AcceptedByUserId,
            invitation.AcceptedAtUtc,
            invitation.CancelledAtUtc,
            memberships);
    }

    private sealed record InvitationConcurrencySeed(
        AuthenticatedTestUser Owner,
        AuthenticatedTestUser InvitedUser,
        Guid AgencyId,
        Guid InvitationId,
        string Token);

    private sealed record InvitationCommittedState(
        AgencyInvitationStatus Status,
        Guid? AcceptedByUserId,
        DateTime? AcceptedAtUtc,
        DateTime? CancelledAtUtc,
        IReadOnlyList<InvitationMembershipState>
            Memberships);

    private sealed record InvitationMembershipState(
        Guid Id,
        AgencyMemberRole Role,
        AgencyMemberStatus Status);
}
