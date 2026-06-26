using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Mappings;

public static class AgencyMappingExtensions
{
    public static AgencyResponse ToResponse(this Agency agency)
    {
        return new AgencyResponse
        {
            Id = agency.Id,
            Name = agency.Name,
            Slug = agency.Slug,
            Description = agency.Description,
            LogoUrl = agency.LogoUrl,
            PhoneNumber = agency.PhoneNumber,
            Email = agency.Email,
            WebsiteUrl = agency.WebsiteUrl,
            AddressLine = agency.AddressLine,
            City = agency.City,
            Municipality = agency.Municipality,
            Status = agency.Status,
            CreatedAtUtc = agency.CreatedAtUtc,
            ModifiedAtUtc = agency.ModifiedAtUtc
        };
    }
}
