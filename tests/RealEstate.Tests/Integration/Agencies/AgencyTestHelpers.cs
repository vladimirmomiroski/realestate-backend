using RealEstate.Domain.Entities;

namespace RealEstate.Tests.Integration.Agencies;

internal static class AgencyTestHelpers
{
    public static Agency CreateAgency()
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
