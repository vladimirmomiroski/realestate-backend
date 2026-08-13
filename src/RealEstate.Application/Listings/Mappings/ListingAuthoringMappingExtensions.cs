using RealEstate.Application.Listings.Dtos;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Mappings;

public static class ListingAuthoringMappingExtensions
{
    public static ListingAuthoringResponse ToAuthoringResponse(
        this Listing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return new ListingAuthoringResponse
        {
            Id = listing.Id,
            CreatedByUserId = listing.CreatedByUserId,
            AgencyId = listing.AgencyId,
            ListingType = listing.ListingType,
            PropertyType = listing.PropertyType,
            Status = listing.Status,
            Price = listing.Price,
            Currency = listing.Currency,
            AreaSquareMeters = listing.AreaSquareMeters,
            Rooms = listing.Rooms,
            Bathrooms = listing.Bathrooms,
            BalconyCount = listing.BalconyCount,
            ParkingSpaces = listing.ParkingSpaces,
            HasBasement = listing.HasBasement,
            IsExchangePossible = listing.IsExchangePossible,
            HeatingType = listing.HeatingType,
            FurnishingStatus = listing.FurnishingStatus,
            Condition = listing.Condition,
            YearRenovated = listing.YearRenovated,
            Orientation = listing.Orientation,
            YearBuilt = listing.YearBuilt,
            Latitude = listing.Latitude,
            Longitude = listing.Longitude,
            LocationPrecision = listing.LocationPrecision,
            GeocodedDisplayName = listing.GeocodedDisplayName,
            LocationConfirmedAtUtc = listing.LocationConfirmedAtUtc,
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
            Translations = listing.Translations
                .OrderBy(
                    translation => translation.LanguageCode,
                    EffectiveTranslationOrdering.LanguageCodeComparer)
                .ThenBy(
                    translation => translation.Id,
                    EffectiveTranslationOrdering.TranslationIdComparer)
                .Select(translation =>
                    new ListingAuthoringTranslationResponse
                    {
                        Id = translation.Id,
                        LanguageCode = translation.LanguageCode,
                        Title = translation.Title,
                        Description = translation.Description,
                        AddressLine = translation.AddressLine,
                        City = translation.City,
                        Municipality = translation.Municipality,
                        Neighborhood = translation.Neighborhood
                    })
                .ToList(),
            Images = listing.Images
                .OrderBy(image => image.SortOrder)
                .ThenBy(
                    image => image.Id,
                    EffectiveTranslationOrdering.TranslationIdComparer)
                .Select(image => new ListingImageResponse
                {
                    Id = image.Id,
                    Url = image.Url,
                    ContentType = image.ContentType,
                    SizeBytes = image.SizeBytes,
                    SortOrder = image.SortOrder,
                    IsPrimary = image.IsPrimary
                })
                .ToList(),
            CreatedAtUtc = listing.CreatedAtUtc,
            ModifiedAtUtc = listing.ModifiedAtUtc
        };
    }
}
