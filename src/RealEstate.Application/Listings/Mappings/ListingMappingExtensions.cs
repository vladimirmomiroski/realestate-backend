using RealEstate.Application.Listings.Dtos;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Mappings;

public static class ListingMappingExtensions
{
    public static ListingResponse ToResponse(this Listing listing, string languageCode)
    {
        var translation =
            EffectiveTranslationOrdering.SelectEffectiveTranslation(
                listing.Translations,
                languageCode);

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
            LocationPrecision = listing.LocationPrecision,
            GeocodedDisplayName = listing.GeocodedDisplayName,
            LocationConfirmedAtUtc = listing.LocationConfirmedAtUtc,
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

    public static PublicListingResponse ToPublicResponse(
        this Listing listing,
        string languageCode)
    {
        ArgumentNullException.ThrowIfNull(listing);

        ListingPublicationReadinessResult readiness =
            listing.EvaluatePublicationReadiness();

        if (!readiness.IsReady)
        {
            throw new PublicListingIntegrityException(
                listing.Id,
                readiness.Violations);
        }

        ListingTranslation? translation =
            EffectiveTranslationOrdering.SelectEffectiveTranslation(
                listing.Translations,
                languageCode);

        if (translation is null ||
            translation.LanguageCode is not string selectedLanguageCode ||
            translation.Title is not string selectedTitle ||
            translation.City is not string selectedCity ||
            translation.Municipality is not string selectedMunicipality ||
            translation.AddressLine is not string selectedAddressLine ||
            translation.Description is not string selectedDescription ||
            listing.Latitude is not decimal selectedLatitude ||
            listing.Longitude is not decimal selectedLongitude ||
            listing.LocationPrecision is not { } selectedLocationPrecision ||
            !ListingTranslationRules.IsCanonicalLanguageCode(
                selectedLanguageCode) ||
            !IsMeaningfulPublicText(selectedTitle) ||
            !IsMeaningfulPublicText(selectedCity) ||
            !IsMeaningfulPublicText(selectedMunicipality) ||
            !IsMeaningfulPublicText(selectedAddressLine) ||
            !IsMeaningfulPublicText(selectedDescription))
        {
            throw new PublicListingIntegrityException(
                listing.Id,
                readiness.Violations);
        }

        var orderedImages = listing.Images
            .OrderBy(image => image.SortOrder)
            .ToList();

        var primaryImageUrl = orderedImages
            .FirstOrDefault(image => image.IsPrimary)?.Url
            ?? orderedImages.FirstOrDefault()?.Url;

        return new PublicListingResponse
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
                    YardAreaSquareMeters =
                        listing.HouseDetails.YardAreaSquareMeters
                },
            AgencyId = listing.AgencyId,
            Status = listing.Status,
            Price = listing.Price,
            Currency = listing.Currency,
            AreaSquareMeters = listing.AreaSquareMeters,
            PricePerSquareMeter =
                Math.Round(listing.CalculatePricePerSquareMeter(), 2),
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
            Latitude = selectedLatitude,
            Longitude = selectedLongitude,
            LocationPrecision = selectedLocationPrecision,
            LanguageCode = selectedLanguageCode,
            Title = selectedTitle,
            Description = selectedDescription,
            AddressLine = selectedAddressLine,
            City = selectedCity,
            Municipality = selectedMunicipality,
            Neighborhood = translation.Neighborhood,
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

    private static bool IsMeaningfulPublicText(string value)
    {
        return value.Length > 0 &&
               value == ListingTranslationRules.NormalizeRequiredText(value);
    }
}
