using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using Microsoft.EntityFrameworkCore;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{Guid.NewGuid()}/invitations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{Guid.NewGuid()}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser nonMember = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenMemberIsAgent()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser agent = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenMemberIsManager()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser manager = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            manager.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Manager);

        _httpClient.AuthorizeAs(manager.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnOk_WhenMemberIsOwner()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.ValueKind.Should().Be(JsonValueKind.Array);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnAgencyInvitations()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        string invitedEmail = $"agent-{Guid.NewGuid():N}@test.com";

        await CreateAgencyInvitationForListAsync(
            agencyId,
            owner.UserId,
            invitedEmail,
            AgencyInvitationStatus.Pending);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(1);

            JsonElement invitation = json.EnumerateArray().Single();

            invitation.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            invitation.GetProperty("email").GetString().Should().Be(invitedEmail);
            invitation.GetProperty("role").GetString().Should().Be(nameof(AgencyMemberRole.Agent));
            invitation.GetProperty("status").GetString().Should().Be(nameof(AgencyInvitationStatus.Pending));
            invitation.GetProperty("invitedByUserId").GetGuid().Should().Be(owner.UserId);
            invitation.TryGetProperty("token", out _).Should().BeFalse();
            invitation.TryGetProperty("code", out _).Should().BeFalse();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldNotReturnInvitationsFromAnotherAgency()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstAgencyId = await CreateAgencyAsAsync(owner);
        Guid secondAgencyId = await CreateAgencyAsAsync(owner);

        string firstEmail = $"first-{Guid.NewGuid():N}@test.com";
        string secondEmail = $"second-{Guid.NewGuid():N}@test.com";

        await CreateAgencyInvitationForListAsync(
            firstAgencyId,
            owner.UserId,
            firstEmail,
            AgencyInvitationStatus.Pending);

        await CreateAgencyInvitationForListAsync(
            secondAgencyId,
            owner.UserId,
            secondEmail,
            AgencyInvitationStatus.Pending);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{firstAgencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(1);

            JsonElement invitation = json.EnumerateArray().Single();

            invitation.GetProperty("agencyId").GetGuid().Should().Be(firstAgencyId);
            invitation.GetProperty("email").GetString().Should().Be(firstEmail);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldApplyEffectiveStatusAndFilters()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);
        Guid otherAgencyId = await CreateAgencyAsAsync(owner);

        DateTime utcNow = DateTime.UtcNow;

        string futurePendingEmail =
            $"future-pending-{Guid.NewGuid():N}@test.com";

        Guid futurePendingId =
            await CreateAgencyInvitationForListAsync(
                agencyId,
                owner.UserId,
                futurePendingEmail,
                AgencyInvitationStatus.Pending,
                utcNow.AddDays(7));

        Guid elapsedPendingId =
            await CreateAgencyInvitationForListAsync(
                agencyId,
                owner.UserId,
                $"elapsed-pending-{Guid.NewGuid():N}@test.com",
                AgencyInvitationStatus.Pending,
                utcNow.AddDays(-1));

        Guid storedExpiredId =
            await CreateAgencyInvitationForListAsync(
                agencyId,
                owner.UserId,
                $"stored-expired-{Guid.NewGuid():N}@test.com",
                AgencyInvitationStatus.Expired,
                utcNow.AddDays(-2));

        Guid storedAcceptedId =
            await CreateAgencyInvitationForListAsync(
                agencyId,
                owner.UserId,
                $"stored-accepted-{Guid.NewGuid():N}@test.com",
                AgencyInvitationStatus.Accepted,
                utcNow.AddDays(7));

        Guid storedCancelledId =
            await CreateAgencyInvitationForListAsync(
                agencyId,
                owner.UserId,
                $"stored-cancelled-{Guid.NewGuid():N}@test.com",
                AgencyInvitationStatus.Cancelled,
                utcNow.AddDays(7));

        await CreateAgencyInvitationForListAsync(
            otherAgencyId,
            owner.UserId,
            $"other-agency-{Guid.NewGuid():N}@test.com",
            AgencyInvitationStatus.Pending,
            utcNow.AddDays(7));

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            JsonElement unfiltered =
                await GetAgencyInvitationsForListAsync(
                    agencyId);

            JsonElement pending =
                await GetAgencyInvitationsForListAsync(
                    agencyId,
                    AgencyInvitationStatus.Pending);

            JsonElement expired =
                await GetAgencyInvitationsForListAsync(
                    agencyId,
                    AgencyInvitationStatus.Expired);

            JsonElement accepted =
                await GetAgencyInvitationsForListAsync(
                    agencyId,
                    AgencyInvitationStatus.Accepted);

            JsonElement cancelled =
                await GetAgencyInvitationsForListAsync(
                    agencyId,
                    AgencyInvitationStatus.Cancelled);

            // Assert
            unfiltered.ValueKind.Should()
                .Be(JsonValueKind.Array);

            GetInvitationIds(unfiltered).Should()
                .BeEquivalentTo(new[]
                {
                    futurePendingId,
                    elapsedPendingId,
                    storedExpiredId,
                    storedAcceptedId,
                    storedCancelledId
                });

            var effectiveStatuses = unfiltered
                .EnumerateArray()
                .ToDictionary(
                    invitation => invitation
                        .GetProperty("id").GetGuid(),
                    invitation => invitation
                        .GetProperty("status").GetString());

            effectiveStatuses[futurePendingId].Should()
                .Be(nameof(AgencyInvitationStatus.Pending));

            effectiveStatuses[elapsedPendingId].Should()
                .Be(nameof(AgencyInvitationStatus.Expired));

            effectiveStatuses[storedExpiredId].Should()
                .Be(nameof(AgencyInvitationStatus.Expired));

            effectiveStatuses[storedAcceptedId].Should()
                .Be(nameof(AgencyInvitationStatus.Accepted));

            effectiveStatuses[storedCancelledId].Should()
                .Be(nameof(AgencyInvitationStatus.Cancelled));

            GetInvitationIds(pending).Should()
                .Equal(futurePendingId);

            GetInvitationIds(expired).Should()
                .BeEquivalentTo(new[]
                {
                    elapsedPendingId,
                    storedExpiredId
                });

            GetInvitationIds(accepted).Should()
                .Equal(storedAcceptedId);

            GetInvitationIds(cancelled).Should()
                .Equal(storedCancelledId);

            JsonElement representative = unfiltered
                .EnumerateArray()
                .Single(invitation =>
                    invitation.GetProperty("id").GetGuid() ==
                    futurePendingId);

            representative.EnumerateObject()
                .Select(property => property.Name)
                .Should()
                .BeEquivalentTo(
                    "id",
                    "agencyId",
                    "email",
                    "role",
                    "status",
                    "invitedByUserId",
                    "acceptedByUserId",
                    "expiresAtUtc",
                    "createdAtUtc",
                    "acceptedAtUtc",
                    "cancelledAtUtc");

            representative.EnumerateObject()
                .Should().HaveCount(11);

            representative.GetProperty("id").GetGuid()
                .Should().Be(futurePendingId);

            representative.GetProperty("agencyId").GetGuid()
                .Should().Be(agencyId);

            representative.GetProperty("email").GetString()
                .Should().Be(futurePendingEmail);

            representative.GetProperty("role").GetString()
                .Should().Be(nameof(AgencyMemberRole.Agent));

            representative.GetProperty("status").ValueKind
                .Should().Be(JsonValueKind.String);

            representative.GetProperty("invitedByUserId")
                .GetGuid().Should().Be(owner.UserId);

            representative.GetProperty("acceptedByUserId")
                .ValueKind.Should().Be(JsonValueKind.Null);

            representative.GetProperty("expiresAtUtc")
                .ValueKind.Should().Be(JsonValueKind.String);

            representative.GetProperty("createdAtUtc")
                .ValueKind.Should().Be(JsonValueKind.String);

            representative.GetProperty("acceptedAtUtc")
                .ValueKind.Should().Be(JsonValueKind.Null);

            representative.GetProperty("cancelledAtUtc")
                .ValueKind.Should().Be(JsonValueKind.Null);

            representative.TryGetProperty("token", out _)
                .Should().BeFalse();

            representative.TryGetProperty("code", out _)
                .Should().BeFalse();

            representative.TryGetProperty(
                    "effectiveStatus",
                    out _)
                .Should().BeFalse();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldPresentElapsedPendingWithoutPersistingUntilReplacement()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyAsAsync(owner);

        string invitedEmail =
            $"stored-status-{Guid.NewGuid():N}@test.com";

        string normalizedEmail =
            invitedEmail.ToUpperInvariant();

        Guid elapsedInvitationId =
            await CreateAgencyInvitationForListAsync(
                agencyId,
                owner.UserId,
                invitedEmail,
                AgencyInvitationStatus.Pending,
                expiresAtUtc:
                    DateTime.UtcNow.AddDays(-1));

        AgencyInvitation originalElapsedInvitation;

        using (IServiceScope originalScope =
               _factory.Services.CreateScope())
        {
            var originalDbContext =
                originalScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            originalElapsedInvitation =
                await originalDbContext.AgencyInvitations
                    .AsNoTracking()
                    .SingleAsync(invitation =>
                        invitation.Id == elapsedInvitationId);
        }

        Guid replacementInvitationId =
            Guid.Empty;

        string replacementToken = string.Empty;
        string replacementCode = string.Empty;

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act: list before an action observes expiry
            HttpResponseMessage beforeResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/invitations");

            // Assert
            beforeResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            JsonElement beforeJson =
                await beforeResponse.Content
                    .ReadFromJsonAsync<JsonElement>();

            JsonElement beforeInvitation =
                beforeJson.EnumerateArray()
                    .Single(invitation =>
                        invitation.GetProperty("id")
                            .GetGuid() ==
                        elapsedInvitationId);

            beforeInvitation.GetProperty("status")
                .GetString()
                .Should()
                .Be(nameof(
                    AgencyInvitationStatus.Expired));

            using (IServiceScope readAssertionScope =
                   _factory.Services.CreateScope())
            {
                var readAssertionDbContext =
                    readAssertionScope.ServiceProvider
                        .GetRequiredService<RealEstateDbContext>();

                AgencyInvitation storedAfterRead =
                    await readAssertionDbContext
                        .AgencyInvitations
                        .AsNoTracking()
                        .SingleAsync(invitation =>
                            invitation.Id ==
                            elapsedInvitationId);

                storedAfterRead.Status.Should()
                    .Be(AgencyInvitationStatus.Pending);

                storedAfterRead.ExpiresAtUtc.Should()
                    .Be(originalElapsedInvitation.ExpiresAtUtc);

                storedAfterRead.CreatedAtUtc.Should()
                    .Be(originalElapsedInvitation.CreatedAtUtc);

                storedAfterRead.ModifiedAtUtc.Should()
                    .Be(originalElapsedInvitation.ModifiedAtUtc);

                storedAfterRead.AcceptedAtUtc.Should()
                    .Be(originalElapsedInvitation.AcceptedAtUtc);

                storedAfterRead.CancelledAtUtc.Should()
                    .Be(originalElapsedInvitation.CancelledAtUtc);
            }

            // Act: create observes elapsed Pending
            HttpResponseMessage createResponse =
                await _httpClient.PostAsJsonAsync(
                    $"/api/agencies/{agencyId}/invitations",
                    CreateValidCreateAgencyInvitationRequest(
                        invitedEmail));

            createResponse.StatusCode.Should()
                .Be(HttpStatusCode.Created);

            JsonElement createdJson =
                await createResponse.Content
                    .ReadFromJsonAsync<JsonElement>();

            replacementInvitationId =
                createdJson.GetProperty("id").GetGuid();

            replacementToken =
                createdJson.GetProperty("token").GetString()!;

            replacementCode =
                createdJson.GetProperty("code").GetString()!;

            replacementToken.Should().NotBeNullOrWhiteSpace();
            replacementCode.Should().NotBeNullOrWhiteSpace();
            replacementToken.Should()
                .NotBe(originalElapsedInvitation.Token);

            // Act: list after replacement
            HttpResponseMessage afterResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/invitations");

            afterResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            JsonElement afterJson =
                await afterResponse.Content
                    .ReadFromJsonAsync<JsonElement>();

            JsonElement oldInvitation =
                afterJson.EnumerateArray()
                    .Single(invitation =>
                        invitation.GetProperty("id")
                            .GetGuid() ==
                        elapsedInvitationId);

            JsonElement replacementInvitation =
                afterJson.EnumerateArray()
                    .Single(invitation =>
                        invitation.GetProperty("id")
                            .GetGuid() ==
                        replacementInvitationId);

            oldInvitation.GetProperty("status")
                .GetString()
                .Should()
                .Be(nameof(
                    AgencyInvitationStatus.Expired));

            replacementInvitation.GetProperty("status")
                .GetString()
                .Should()
                .Be(nameof(
                    AgencyInvitationStatus.Pending));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<AgencyInvitation> storedInvitations =
            await dbContext.AgencyInvitations
                .AsNoTracking()
                .Where(invitation =>
                    invitation.AgencyId == agencyId &&
                    invitation.NormalizedEmail ==
                        normalizedEmail)
                .ToListAsync();

        storedInvitations.Should().HaveCount(2);

        AgencyInvitation storedElapsedInvitation =
            storedInvitations.Single(invitation =>
                invitation.Id == elapsedInvitationId);

        storedElapsedInvitation.Status.Should()
            .Be(AgencyInvitationStatus.Expired);

        AgencyInvitation storedReplacementInvitation =
            storedInvitations.Single(invitation =>
                invitation.Id == replacementInvitationId);

        storedReplacementInvitation.Status.Should()
            .Be(AgencyInvitationStatus.Pending);

        storedReplacementInvitation.Token.Should()
            .Be(replacementToken);

        storedReplacementInvitation.Code.Should()
            .Be(replacementCode);

        storedInvitations.Count(invitation =>
                invitation.Status ==
                AgencyInvitationStatus.Pending)
            .Should().Be(1);
    }

    private async Task<JsonElement>
        GetAgencyInvitationsForListAsync(
            Guid agencyId,
            AgencyInvitationStatus? status = null)
    {
        string statusQuery = status.HasValue
            ? $"?status={status.Value}"
            : string.Empty;

        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations" +
                statusQuery);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content
            .ReadFromJsonAsync<JsonElement>();
    }

    private static IReadOnlyList<Guid> GetInvitationIds(
        JsonElement invitations)
    {
        return invitations.EnumerateArray()
            .Select(invitation =>
                invitation.GetProperty("id").GetGuid())
            .ToList();
    }

    private async Task<Guid>
     CreateAgencyInvitationForListAsync(
         Guid agencyId,
         Guid invitedByUserId,
         string email,
         AgencyInvitationStatus status,
         DateTime? expiresAtUtc = null)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        DateTime utcNow = DateTime.UtcNow;

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail:
                email.ToUpperInvariant(),
            token: Guid.NewGuid().ToString("N"),
            code: Random.Shared
                .Next(0, 1_000_000)
                .ToString("D6"),
            role: AgencyMemberRole.Agent,
            invitedByUserId: invitedByUserId,
            expiresAtUtc:
                expiresAtUtc ??
                (
                    status ==
                    AgencyInvitationStatus.Expired
                        ? utcNow.AddDays(-1)
                        : utcNow.AddDays(7)
                ));

        if (status ==
            AgencyInvitationStatus.Accepted)
        {
            invitation.Accept(
                invitedByUserId,
                utcNow);
        }

        if (status ==
            AgencyInvitationStatus.Cancelled)
        {
            invitation.Cancel(utcNow);
        }

        if (status ==
            AgencyInvitationStatus.Expired)
        {
            invitation.MarkExpired(utcNow);
        }

        dbContext.AgencyInvitations.Add(
            invitation);

        await dbContext.SaveChangesAsync();

        return invitation.Id;
    }
}
