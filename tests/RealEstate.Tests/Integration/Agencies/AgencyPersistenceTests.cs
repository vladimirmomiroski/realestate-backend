using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed class AgencyPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public AgencyPersistenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task Can_save_and_load_agency()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var agency = new Agency(
            name: "Dom Real Estate",
            slug: "dom-real-estate",
            description: "Real estate agency in Skopje.",
            phoneNumber: "+38970123456",
            email: "agency@test.com",
            websiteUrl: "https://agency.test",
            addressLine: "Partizanska 1",
            city: "Skopje",
            municipality: "Centar");

        dbContext.Agencies.Add(agency);
        await dbContext.SaveChangesAsync();

        var savedAgency = await dbContext.Agencies.FindAsync(agency.Id);

        savedAgency.Should().NotBeNull();
        savedAgency!.Name.Should().Be("Dom Real Estate");
        savedAgency.Slug.Should().Be("dom-real-estate");
        savedAgency.Status.Should().Be(AgencyStatus.PendingVerification);
        savedAgency.CreatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task Can_save_and_load_agency_with_member()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var agency = CreateAgency();

            agency.AddMember(user.UserId, AgencyMemberRole.Owner);

            dbContext.Agencies.Add(agency);

            await dbContext.SaveChangesAsync();

            agencyId = agency.Id;
        }

        // Act
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var savedAgency = await dbContext.Agencies
                .Include(agency => agency.Members)
                .SingleAsync(agency => agency.Id == agencyId);

            // Assert
            savedAgency.Members.Should().ContainSingle();

            var member = savedAgency.Members.Single();

            member.AgencyId.Should().Be(agencyId);
            member.UserId.Should().Be(user.UserId);
            member.Role.Should().Be(AgencyMemberRole.Owner);
            member.Status.Should().Be(AgencyMemberStatus.Active);
            member.CreatedAtUtc.Should().NotBe(default);
        }
    }

    private static Agency CreateAgency()
    {
        return new Agency(
            name: $"Dom Real Estate {Guid.NewGuid():N}",
            slug: $"dom-real-estate-{Guid.NewGuid():N}",
            description: "Real estate agency in Skopje.",
            phoneNumber: "+38970123456",
            email: "agency@test.com",
            websiteUrl: "https://agency.test",
            addressLine: "Partizanska 1",
            city: "Skopje",
            municipality: "Centar");
    }
}