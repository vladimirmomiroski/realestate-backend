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

        var orderedImages = listing.Images
            .OrderBy(image => image.SortOrder)
            .ToList();

        var primaryImageUrl = orderedImages
            .FirstOrDefault(image => image.IsPrimary)?.Url
            ?? orderedImages.FirstOrDefault()?.Url;

        return new ListingResponse
        {
            Id = listing.Id,
            ListingType = listing.ListingType,
            PropertyType = listing.PropertyType,
            ApartmentDetails = listing.ApartmentDetails is null
    ? null
    : new ListingApartmentDetailsResponse
    {
        ApartmentType = listing.ApartmentDetails.ApartmentType,
        Floor = listing.ApartmentDetails.Floor,
        TotalFloors = listing.ApartmentDetails.TotalFloors,
        HasElevator = listing.ApartmentDetails.HasElevator
    },

            HouseDetails = listing.HouseDetails is null
    ? null
    : new ListingHouseDetailsResponse
    {
        HouseType = listing.HouseDetails.HouseType,
        NumberOfFloors = listing.HouseDetails.NumberOfFloors,
        YardAreaSquareMeters = listing.HouseDetails.YardAreaSquareMeters
    },
            AgencyId = listing.AgencyId,
            Status = listing.Status,
            Price = listing.Price,
            Currency = listing.Currency,
            AreaSquareMeters = listing.AreaSquareMeters,
            PricePerSquareMeter = Math.Round(listing.CalculatePricePerSquareMeter(), 2),
            Rooms = listing.Rooms,
            Bathrooms = listing.Bathrooms,
            YearBuilt = listing.YearBuilt,
            BalconyCount = listing.BalconyCount,
            ParkingSpaces = listing.ParkingSpaces,
            HasBasement = listing.HasBasement,
            IsExchangePossible = listing.IsExchangePossible,
            HeatingType = listing.HeatingType,
            FurnishingStatus = listing.FurnishingStatus,
            Condition = listing.Condition,
            YearRenovated = listing.YearRenovated,
            Orientation = listing.Orientation,
            Latitude = listing.Latitude,
            Longitude = listing.Longitude,
            LanguageCode = translation?.LanguageCode,
            Title = translation?.Title,
            Description = translation?.Description,
            AddressLine = translation?.AddressLine,
            City = translation?.City,
            Municipality = translation?.Municipality,
            Neighborhood = translation?.Neighborhood,
            PrimaryImageUrl = primaryImageUrl,
            Images = orderedImages
                .Select(image => new ListingImageResponse
                {
                    Id = image.Id,
                    Url = image.Url,
                    ContentType = image.ContentType,
                    SizeBytes = image.SizeBytes,
                    SortOrder = image.SortOrder,
                    IsPrimary = image.IsPrimary
                })
                .ToList()
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