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

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    private static readonly TimeSpan OwnerConcurrencyTimeout =
        TimeSpan.FromSeconds(20);

    [Fact]
    public async Task AgencyOwnerConcurrency_DemotionVersusDemotion_ShouldLeaveExactlyOneActiveOwner()
    {
        // Arrange
        AuthenticatedTestUser firstOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        AuthenticatedTestUser secondOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyWithMembersAsync(
                firstOwner.UserId,
                secondOwner.UserId,
                AgencyMemberStatus.Active,
                AgencyMemberRole.Owner);

        Guid firstOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                firstOwner.UserId);

        Guid secondOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                secondOwner.UserId);

        using HttpClient firstClient =
            CreateOwnerConcurrencyClient(
                firstOwner.AccessToken);

        using HttpClient secondClient =
            CreateOwnerConcurrencyClient(
                secondOwner.AccessToken);

        var demoteRequest = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        (
            HttpResponseMessage firstResponse,
            HttpResponseMessage secondResponse
        ) = await ExecuteContestedOwnerMutationAsync(
            firstTargetMemberId: secondOwnerMemberId,
            firstRequest: cancellationToken =>
                firstClient.PutAsJsonAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{secondOwnerMemberId}/role",
                    demoteRequest,
                    cancellationToken),
            secondRequest: cancellationToken =>
                secondClient.PutAsJsonAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{firstOwnerMemberId}/role",
                    demoteRequest,
                    cancellationToken));

        using (firstResponse)
        using (secondResponse)
        {
            // Assert
            firstResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.NoContent);

            secondResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.Forbidden);
        }

        await AssertCommittedAgencyMemberStateAsync(
            agencyId,
            expectedActiveOwnerCount: 1,
            new ExpectedMemberState(
                firstOwner.UserId,
                AgencyMemberRole.Owner,
                AgencyMemberStatus.Active),
            new ExpectedMemberState(
                secondOwner.UserId,
                AgencyMemberRole.Agent,
                AgencyMemberStatus.Active));
    }

    [Fact]
    public async Task AgencyOwnerConcurrency_DisableVersusDisable_ShouldLeaveExactlyOneActiveOwner()
    {
        // Arrange
        AuthenticatedTestUser firstOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        AuthenticatedTestUser secondOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyWithMembersAsync(
                firstOwner.UserId,
                secondOwner.UserId,
                AgencyMemberStatus.Active,
                AgencyMemberRole.Owner);

        Guid firstOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                firstOwner.UserId);

        Guid secondOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                secondOwner.UserId);

        using HttpClient firstClient =
            CreateOwnerConcurrencyClient(
                firstOwner.AccessToken);

        using HttpClient secondClient =
            CreateOwnerConcurrencyClient(
                secondOwner.AccessToken);

        // Act
        (
            HttpResponseMessage firstResponse,
            HttpResponseMessage secondResponse
        ) = await ExecuteContestedOwnerMutationAsync(
            firstTargetMemberId: secondOwnerMemberId,
            firstRequest: cancellationToken =>
                firstClient.PutAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{secondOwnerMemberId}/disable",
                    content: null,
                    cancellationToken),
            secondRequest: cancellationToken =>
                secondClient.PutAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{firstOwnerMemberId}/disable",
                    content: null,
                    cancellationToken));

        using (firstResponse)
        using (secondResponse)
        {
            // Assert
            firstResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.NoContent);

            secondResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.Forbidden);
        }

        await AssertCommittedAgencyMemberStateAsync(
            agencyId,
            expectedActiveOwnerCount: 1,
            new ExpectedMemberState(
                firstOwner.UserId,
                AgencyMemberRole.Owner,
                AgencyMemberStatus.Active),
            new ExpectedMemberState(
                secondOwner.UserId,
                AgencyMemberRole.Owner,
                AgencyMemberStatus.Disabled));
    }

    [Fact]
    public async Task AgencyOwnerConcurrency_DemotionVersusDisable_ShouldLeaveExactlyOneActiveOwner()
    {
        // Arrange
        AuthenticatedTestUser firstOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        AuthenticatedTestUser secondOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyWithMembersAsync(
                firstOwner.UserId,
                secondOwner.UserId,
                AgencyMemberStatus.Active,
                AgencyMemberRole.Owner);

        Guid firstOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                firstOwner.UserId);

        Guid secondOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                secondOwner.UserId);

        using HttpClient firstClient =
            CreateOwnerConcurrencyClient(
                firstOwner.AccessToken);

        using HttpClient secondClient =
            CreateOwnerConcurrencyClient(
                secondOwner.AccessToken);

        var demoteRequest = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        (
            HttpResponseMessage firstResponse,
            HttpResponseMessage secondResponse
        ) = await ExecuteContestedOwnerMutationAsync(
            firstTargetMemberId: secondOwnerMemberId,
            firstRequest: cancellationToken =>
                firstClient.PutAsJsonAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{secondOwnerMemberId}/role",
                    demoteRequest,
                    cancellationToken),
            secondRequest: cancellationToken =>
                secondClient.PutAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{firstOwnerMemberId}/disable",
                    content: null,
                    cancellationToken));

        using (firstResponse)
        using (secondResponse)
        {
            // Assert
            firstResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.NoContent);

            secondResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.Forbidden);
        }

        await AssertCommittedAgencyMemberStateAsync(
            agencyId,
            expectedActiveOwnerCount: 1,
            new ExpectedMemberState(
                firstOwner.UserId,
                AgencyMemberRole.Owner,
                AgencyMemberStatus.Active),
            new ExpectedMemberState(
                secondOwner.UserId,
                AgencyMemberRole.Agent,
                AgencyMemberStatus.Active));
    }

    [Fact]
    public async Task AgencyOwnerConcurrency_SelfDemotionAfterWaiting_ShouldUseRefreshedOwnerCount()
    {
        // Arrange
        AuthenticatedTestUser firstOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        AuthenticatedTestUser secondOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateAgencyWithMembersAsync(
                firstOwner.UserId,
                secondOwner.UserId,
                AgencyMemberStatus.Active,
                AgencyMemberRole.Owner);

        Guid firstOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                firstOwner.UserId);

        Guid secondOwnerMemberId =
            await GetAgencyMemberIdAsync(
                agencyId,
                secondOwner.UserId);

        using HttpClient firstClient =
            CreateOwnerConcurrencyClient(
                firstOwner.AccessToken);

        using HttpClient secondClient =
            CreateOwnerConcurrencyClient(
                secondOwner.AccessToken);

        var demoteRequest = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        (
            HttpResponseMessage firstResponse,
            HttpResponseMessage secondResponse
        ) = await ExecuteContestedOwnerMutationAsync(
            firstTargetMemberId: firstOwnerMemberId,
            firstRequest: cancellationToken =>
                firstClient.PutAsJsonAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{firstOwnerMemberId}/role",
                    demoteRequest,
                    cancellationToken),
            secondRequest: cancellationToken =>
                secondClient.PutAsJsonAsync(
                    $"/api/agencies/{agencyId}" +
                    $"/members/{secondOwnerMemberId}/role",
                    demoteRequest,
                    cancellationToken));

        using (firstResponse)
        using (secondResponse)
        {
            // Assert
            firstResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.NoContent);

            secondResponse.StatusCode
                .Should()
                .Be(HttpStatusCode.Conflict);

            await AssertResourceStateConflictAsync(
                secondResponse,
                $"/api/agencies/{agencyId}/members/{secondOwnerMemberId}/role");
        }

        await AssertCommittedAgencyMemberStateAsync(
            agencyId,
            expectedActiveOwnerCount: 1,
            new ExpectedMemberState(
                firstOwner.UserId,
                AgencyMemberRole.Agent,
                AgencyMemberStatus.Active),
            new ExpectedMemberState(
                secondOwner.UserId,
                AgencyMemberRole.Owner,
                AgencyMemberStatus.Active));
    }

    private HttpClient CreateOwnerConcurrencyClient(
        string accessToken)
    {
        HttpClient client =
            _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        return client;
    }

    private async Task<(
        HttpResponseMessage FirstResponse,
        HttpResponseMessage SecondResponse)>
        ExecuteContestedOwnerMutationAsync(
            Guid firstTargetMemberId,
            Func<CancellationToken, Task<HttpResponseMessage>>
                firstRequest,
            Func<CancellationToken, Task<HttpResponseMessage>>
                secondRequest)
    {
        using var timeoutSource =
            new CancellationTokenSource(
                OwnerConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeoutSource.Token;

        using IServiceScope gateScope =
            _factory.Services.CreateScope();

        var gateDbContext =
            gateScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        await using IDbContextTransaction gateTransaction =
            await gateDbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

        DbConnection gateConnection =
            gateDbContext.Database.GetDbConnection();

        DbTransaction gateDbTransaction =
            gateTransaction.GetDbTransaction();

        int gateBackendPid =
            await GetBackendPidAsync(
                gateConnection,
                gateDbTransaction,
                cancellationToken);

        await LockAgencyMemberAsync(
            gateConnection,
            gateDbTransaction,
            firstTargetMemberId,
            cancellationToken);

        using IServiceScope observerScope =
            _factory.Services.CreateScope();

        var observerDbContext =
            observerScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        DbConnection observerConnection =
            observerDbContext.Database.GetDbConnection();

        if (observerConnection.State !=
            ConnectionState.Open)
        {
            await observerConnection.OpenAsync(
                cancellationToken);
        }

        bool gateReleased = false;

        try
        {
            Task<HttpResponseMessage> firstRequestTask =
                firstRequest(cancellationToken);

            int firstRequestBackendPid =
                await WaitForBlockedBackendAsync(
                    observerConnection,
                    blockingBackendPid: gateBackendPid,
                    queryPattern:
                        "%UPDATE \"AgencyMembers\"%",
                    requireForUpdate: false,
                    cancellationToken);

            Task<HttpResponseMessage> secondRequestTask =
                secondRequest(cancellationToken);

            await WaitForBlockedBackendAsync(
                observerConnection,
                blockingBackendPid:
                    firstRequestBackendPid,
                queryPattern:
                    "%FROM \"Agencies\"%",
                requireForUpdate: true,
                cancellationToken);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            HttpResponseMessage firstResponse =
                await firstRequestTask.WaitAsync(
                    cancellationToken);

            HttpResponseMessage secondResponse =
                await secondRequestTask.WaitAsync(
                    cancellationToken);

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
        }
    }

    private static async Task<int> GetBackendPidAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using DbCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText =
            "SELECT pg_backend_pid();";

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (result is null ||
            result is DBNull)
        {
            throw new InvalidOperationException(
                "PostgreSQL backend PID could not be resolved.");
        }

        return Convert.ToInt32(result);
    }

    private static async Task LockAgencyMemberAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using DbCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandTimeout = 2;
        command.CommandText =
            """
            SELECT "Id"
            FROM "AgencyMembers"
            WHERE "Id" = @memberId
            FOR UPDATE;
            """;

        AddParameter(
            command,
            parameterName: "memberId",
            DbType.Guid,
            memberId);

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (result is null ||
            result is DBNull)
        {
            throw new InvalidOperationException(
                "The AgencyMember test gate could not acquire its row lock.");
        }
    }

    private static async Task<int>
        WaitForBlockedBackendAsync(
            DbConnection observerConnection,
            int blockingBackendPid,
            string queryPattern,
            bool requireForUpdate,
            CancellationToken cancellationToken)
    {
        while (true)
        {
            await using DbCommand command =
                observerConnection.CreateCommand();

            command.CommandTimeout = 2;
            command.CommandText =
                """
                SELECT activity.pid
                FROM pg_stat_activity AS activity
                WHERE activity.datname = current_database()
                  AND activity.pid <> pg_backend_pid()
                  AND activity.state = 'active'
                  AND activity.wait_event_type = 'Lock'
                  AND activity.query ILIKE @queryPattern
                  AND (
                      @requireForUpdate = FALSE
                      OR activity.query ILIKE '%FOR UPDATE%'
                  )
                  AND @blockingBackendPid =
                      ANY(pg_blocking_pids(activity.pid))
                ORDER BY activity.pid
                LIMIT 1;
                """;

            AddParameter(
                command,
                parameterName: "queryPattern",
                DbType.String,
                queryPattern);

            AddParameter(
                command,
                parameterName: "requireForUpdate",
                DbType.Boolean,
                requireForUpdate);

            AddParameter(
                command,
                parameterName: "blockingBackendPid",
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
                millisecondsDelay: 25,
                cancellationToken);
        }
    }

    private async Task AssertCommittedAgencyMemberStateAsync(
        Guid agencyId,
        int expectedActiveOwnerCount,
        params ExpectedMemberState[] expectedMembers)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<AgencyMember> members =
            await dbContext.Set<AgencyMember>()
                .AsNoTracking()
                .Where(member =>
                    member.AgencyId == agencyId)
                .ToListAsync();

        members.Count(member =>
                member.Role ==
                    AgencyMemberRole.Owner &&
                member.Status ==
                    AgencyMemberStatus.Active)
            .Should()
            .Be(expectedActiveOwnerCount);

        foreach (ExpectedMemberState expectedMember
                 in expectedMembers)
        {
            AgencyMember savedMember =
                members.Single(member =>
                    member.UserId ==
                    expectedMember.UserId);

            savedMember.Role
                .Should()
                .Be(expectedMember.Role);

            savedMember.Status
                .Should()
                .Be(expectedMember.Status);
        }
    }

    private static void AddParameter(
        DbCommand command,
        string parameterName,
        DbType dbType,
        object value)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName =
            parameterName;

        parameter.DbType =
            dbType;

        parameter.Value =
            value;

        command.Parameters.Add(parameter);
    }

    private sealed record ExpectedMemberState(
        Guid UserId,
        AgencyMemberRole Role,
        AgencyMemberStatus Status);
}
