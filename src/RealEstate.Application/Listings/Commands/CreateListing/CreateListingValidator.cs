using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingValidator
{
    public sealed record ValidationFailure(string Key, string Error);

    public const string InvalidCurrencyError =
    "Currency must contain exactly three ASCII letters.";

    public const string CoordinatePairError =
        "Latitude and longitude must both be provided or both be omitted.";

    public const string LatitudeOutOfRangeError =
        "Latitude must be between -90 and 90.";

    public const string LongitudeOutOfRangeError =
        "Longitude must be between -180 and 180.";

    public string? Validate(CreateListingRequest request)
    {
        return ValidateWithKey(request)?.Error;
    }

    public ValidationFailure? ValidateWithKey(CreateListingRequest request)
    {
        if (request.Price <= 0)
        {
            return Failure("price", "Price must be greater than zero.");
        }

        if (request.AreaSquareMeters <= 0)
        {
            return Failure("areaSquareMeters", "Area must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return Failure("currency", "Currency is required.");
        }

        string trimmedCurrency =
            request.Currency.Trim();

        if (!IsValidCurrency(trimmedCurrency))
        {
            return Failure("currency", InvalidCurrencyError);
        }

        bool hasLatitude =
            request.Latitude.HasValue;

        bool hasLongitude =
            request.Longitude.HasValue;

        if (hasLatitude != hasLongitude)
        {
            return Failure("request", CoordinatePairError);
        }

        if (request.Latitude is < -90m or > 90m)
        {
            return Failure("latitude", LatitudeOutOfRangeError);
        }

        if (request.Longitude is < -180m or > 180m)
        {
            return Failure("longitude", LongitudeOutOfRangeError);
        }

        if (request.Translations is null || request.Translations.Count == 0)
        {
            return Failure("translations", "At least one translation is required.");
        }

        int missingLanguageIndex = request.Translations.FindIndex(
            translation => string.IsNullOrWhiteSpace(translation.LanguageCode));

        if (missingLanguageIndex >= 0)
        {
            return Failure(
                $"translations[{missingLanguageIndex}].languageCode",
                "Translation language code is required.");
        }

        int missingTitleIndex = request.Translations.FindIndex(
            translation => string.IsNullOrWhiteSpace(translation.Title));

        if (missingTitleIndex >= 0)
        {
            return Failure(
                $"translations[{missingTitleIndex}].title",
                "Translation title is required.");
        }

        var hasDuplicateLanguages = request.Translations
            .GroupBy(translation => NormalizeLanguageCode(translation.LanguageCode))
            .Any(group => group.Count() > 1);

        if (hasDuplicateLanguages)
        {
            return Failure(
                "translations",
                "Duplicate translation languages are not allowed.");
        }

        if (request.BalconyCount is < 0)
        {
            return Failure("balconyCount", "Balcony count cannot be negative.");
        }

        if (request.ParkingSpaces is < 0)
        {
            return Failure("parkingSpaces", "Parking spaces cannot be negative.");
        }

        if (request.YearRenovated is < 1800 or > 2100)
        {
            return Failure("yearRenovated", "Year renovated is not valid.");
        }

        if (request.YearRenovated.HasValue &&
            request.YearBuilt.HasValue &&
            request.YearRenovated.Value < request.YearBuilt.Value)
        {
            return Failure(
                "request",
                "Year renovated cannot be earlier than year built.");
        }

        if (request.PropertyType == PropertyType.Apartment)
        {
            if (request.ApartmentDetails is null)
            {
                return Failure(
                    "apartmentDetails",
                    "Apartment details are required for apartment listings.");
            }

            if (request.HouseDetails is not null)
            {
                return Failure(
                    "request",
                    "House details are not allowed for apartment listings.");
            }

            if (request.ApartmentDetails.Floor is < 0)
            {
                return Failure(
                    "apartmentDetails.floor",
                    "Floor cannot be negative.");
            }

            if (request.ApartmentDetails.TotalFloors is < 0)
            {
                return Failure(
                    "apartmentDetails.totalFloors",
                    "Total floors cannot be negative.");
            }

            if (request.ApartmentDetails.Floor.HasValue &&
                request.ApartmentDetails.TotalFloors.HasValue &&
                request.ApartmentDetails.Floor.Value > request.ApartmentDetails.TotalFloors.Value)
            {
                return Failure(
                    "request",
                    "Floor cannot be greater than total floors.");
            }
        }

        if (request.PropertyType == PropertyType.House)
        {
            if (request.HouseDetails is null)
            {
                return Failure(
                    "houseDetails",
                    "House details are required for house listings.");
            }

            if (request.ApartmentDetails is not null)
            {
                return Failure(
                    "request",
                    "Apartment details are not allowed for house listings.");
            }

            if (request.HouseDetails.NumberOfFloors is < 0)
            {
                return Failure(
                    "houseDetails.numberOfFloors",
                    "Number of floors cannot be negative.");
            }

            if (request.HouseDetails.YardAreaSquareMeters is < 0)
            {
                return Failure(
                    "houseDetails.yardAreaSquareMeters",
                    "Yard area cannot be negative.");
            }
        }

        if (request.AgencyId == Guid.Empty)
        {
            return Failure("agencyId", "Agency id cannot be empty.");
        }


        return null;
    }

    private static ValidationFailure Failure(string key, string error)
    {
        return new ValidationFailure(key, error);
    }

    private static bool IsValidCurrency(string currency)
    {
        return currency.Length == 3 &&
               currency.All(character =>
                   character is >= 'A' and <= 'Z' ||
                   character is >= 'a' and <= 'z');
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        return languageCode.Trim().ToLowerInvariant();
    }
}
