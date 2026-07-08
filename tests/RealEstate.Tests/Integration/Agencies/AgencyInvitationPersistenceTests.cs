using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

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
