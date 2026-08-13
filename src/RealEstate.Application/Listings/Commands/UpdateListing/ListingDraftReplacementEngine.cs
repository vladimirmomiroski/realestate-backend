using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class ListingDraftReplacementEngine
{
    public void Apply(
        IListingAuthoringWriteScope writeScope,
        UpdateListingRequest request)
    {
        ArgumentNullException.ThrowIfNull(writeScope);
        ArgumentNullException.ThrowIfNull(request);

        Listing listing = writeScope.Listing;

        if (listing.Status != ListingStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft listings can be replaced.");
        }

        bool locationTextChanged = HasLocationTextChanged(
            listing.Translations,
            request.Translations);

        if (locationTextChanged)
        {
            listing.ClearLocation();
        }

        ApplyRoot(listing, request);
        ReconcileTranslations(writeScope, request.Translations);
        ReplaceSubtypeDetails(listing, request);

        writeScope.MarkListingModified();
    }

    private static bool HasLocationTextChanged(
        IEnumerable<ListingTranslation> existingTranslations,
        IEnumerable<UpdateListingTranslationRequest> requestedTranslations)
    {
        Dictionary<string, LocationText> existingByLanguage =
            existingTranslations.ToDictionary(
                translation => ListingTranslationRules.NormalizeLanguageCode(
                    translation.LanguageCode),
                translation => LocationText.From(
                    translation.City,
                    translation.Municipality,
                    translation.AddressLine,
                    translation.Neighborhood),
                StringComparer.Ordinal);

        Dictionary<string, LocationText> requestedByLanguage =
            requestedTranslations.ToDictionary(
                translation => ListingTranslationRules.NormalizeLanguageCode(
                    translation.LanguageCode),
                translation => LocationText.From(
                    translation.City,
                    translation.Municipality,
                    translation.AddressLine,
                    translation.Neighborhood),
                StringComparer.Ordinal);

        if (existingByLanguage.Count != requestedByLanguage.Count)
        {
            return true;
        }

        foreach ((string languageCode, LocationText existing) in
            existingByLanguage)
        {
            if (!requestedByLanguage.TryGetValue(
                    languageCode,
                    out LocationText? requested) ||
                existing != requested)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyRoot(
        Listing listing,
        UpdateListingRequest request)
    {
        listing.ListingType = request.ListingType;
        listing.PropertyType = request.PropertyType;
        listing.Price = request.Price;
        listing.Currency = request.Currency;
        listing.AreaSquareMeters = request.AreaSquareMeters;
        listing.Rooms = request.Rooms;
        listing.Bathrooms = request.Bathrooms;
        listing.BalconyCount = request.BalconyCount;
        listing.ParkingSpaces = request.ParkingSpaces;
        listing.HasBasement = request.HasBasement;
        listing.IsExchangePossible = request.IsExchangePossible;
        listing.HeatingType = request.HeatingType;
        listing.FurnishingStatus = request.FurnishingStatus;
        listing.Condition = request.Condition;
        listing.YearRenovated = request.YearRenovated;
        listing.Orientation = request.Orientation;
        listing.YearBuilt = request.YearBuilt;
    }

    private static void ReconcileTranslations(
        IListingAuthoringWriteScope writeScope,
        IReadOnlyList<UpdateListingTranslationRequest> requestedTranslations)
    {
        Listing listing = writeScope.Listing;

        Dictionary<string, ListingTranslation> existingByLanguage =
            listing.Translations.ToDictionary(
                translation => ListingTranslationRules.NormalizeLanguageCode(
                    translation.LanguageCode),
                StringComparer.Ordinal);

        Dictionary<string, UpdateListingTranslationRequest> requestedByLanguage =
            requestedTranslations.ToDictionary(
                translation => ListingTranslationRules.NormalizeLanguageCode(
                    translation.LanguageCode),
                StringComparer.Ordinal);

        foreach (ListingTranslation existing in listing.Translations.ToList())
        {
            string languageCode =
                ListingTranslationRules.NormalizeLanguageCode(
                    existing.LanguageCode);

            if (!requestedByLanguage.ContainsKey(languageCode))
            {
                writeScope.RemoveTranslation(existing);
            }
        }

        foreach ((string languageCode, UpdateListingTranslationRequest requested)
            in requestedByLanguage.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!existingByLanguage.TryGetValue(
                    languageCode,
                    out ListingTranslation? translation))
            {
                translation = new ListingTranslation
                {
                    Id = Guid.NewGuid(),
                    ListingId = listing.Id,
                    Listing = listing
                };

                writeScope.AddTranslation(translation);
            }

            translation.LanguageCode = languageCode;
            translation.Title = requested.Title;
            translation.Description = requested.Description;
            translation.AddressLine = requested.AddressLine;
            translation.City = requested.City;
            translation.Municipality = requested.Municipality;
            translation.Neighborhood = requested.Neighborhood;
        }
    }

    private static void ReplaceSubtypeDetails(
        Listing listing,
        UpdateListingRequest request)
    {
        if (request.PropertyType == PropertyType.Apartment)
        {
            UpdateListingApartmentDetailsRequest requested =
                request.ApartmentDetails
                ?? throw new InvalidOperationException(
                    "Validated apartment replacement requires apartment details.");

            listing.HouseDetails = null;

            listing.ApartmentDetails ??= new ListingApartmentDetails
            {
                ListingId = listing.Id,
                Listing = listing
            };

            listing.ApartmentDetails.ApartmentType = requested.ApartmentType;
            listing.ApartmentDetails.Floor = requested.Floor;
            listing.ApartmentDetails.TotalFloors = requested.TotalFloors;
            listing.ApartmentDetails.HasElevator = requested.HasElevator;

            return;
        }

        UpdateListingHouseDetailsRequest houseRequested =
            request.HouseDetails
            ?? throw new InvalidOperationException(
                "Validated house replacement requires house details.");

        listing.ApartmentDetails = null;

        listing.HouseDetails ??= new ListingHouseDetails
        {
            ListingId = listing.Id,
            Listing = listing
        };

        listing.HouseDetails.HouseType = houseRequested.HouseType;
        listing.HouseDetails.NumberOfFloors = houseRequested.NumberOfFloors;
        listing.HouseDetails.YardAreaSquareMeters =
            houseRequested.YardAreaSquareMeters;
    }

    private sealed record LocationText(
        string? City,
        string? Municipality,
        string? AddressLine,
        string? Neighborhood)
    {
        public static LocationText From(
            string? city,
            string? municipality,
            string? addressLine,
            string? neighborhood)
        {
            return new LocationText(
                ListingTranslationRules.NormalizeOptionalText(city),
                ListingTranslationRules.NormalizeOptionalText(municipality),
                ListingTranslationRules.NormalizeOptionalText(addressLine),
                ListingTranslationRules.NormalizeOptionalText(neighborhood));
        }
    }
}
