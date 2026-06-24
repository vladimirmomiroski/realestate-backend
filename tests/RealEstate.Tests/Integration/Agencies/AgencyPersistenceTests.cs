using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Agencies;

public sealed class AgencyPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AgencyPersistenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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
}