using RealEstate.Application.Listings.Dtos;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Mappings;

public static class ListingMappingExtensions
{
    public static ListingResponse ToResponse(this Listing listing, string languageCode)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        var translation = listing.Translations
            .FirstOrDefault(translation =>
                translation.LanguageCode.Equals(normalizedLanguageCode, StringComparison.OrdinalIgnoreCase))
            ?? listing.Translations.FirstOrDefault();

        return new ListingResponse
        {
            Id = listing.Id,
            ListingType = listing.ListingType,
            PropertyType = listing.PropertyType,
            Status = listing.Status,
            Price = listing.Price,
            Currency = listing.Currency,
            AreaSquareMeters = listing.AreaSquareMeters,
            PricePerSquareMeter = listing.CalculatePricePerSquareMeter(),
            Rooms = listing.Rooms,
            Bathrooms = listing.Bathrooms,
            Floor = listing.Floor,
            TotalFloors = listing.TotalFloors,
            YearBuilt = listing.YearBuilt,
            Latitude = listing.Latitude,
            Longitude = listing.Longitude,
            LanguageCode = translation?.LanguageCode,
            Title = translation?.Title,
            Description = translation?.Description,
            AddressLine = translation?.AddressLine,
            City = translation?.City,
            Neighborhood = translation?.Neighborhood
        };
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "mk";
        }

        return languageCode.Trim().ToLowerInvariant();
    }
}