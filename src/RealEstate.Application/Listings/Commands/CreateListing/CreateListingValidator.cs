using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingValidator
{
    public sealed record ValidationFailure(string Key, string Error);

    public const string InvalidCurrencyError =
    "Currency must contain exactly three ASCII letters.";

    public const string InvalidListingTypeError =
        "Listing type must be a currently supported value.";

    public const string InvalidPropertyTypeError =
        "Property type must be a currently supported value.";

    public const string InvalidLanguageCodeError =
        "Translation language code has an invalid format.";

    public string? Validate(CreateListingRequest request)
    {
        return ValidateWithKey(request)?.Error;
    }

    public ValidationFailure? ValidateWithKey(CreateListingRequest request)
    {
        if (!Enum.IsDefined(request.ListingType))
        {
            return Failure("listingType", InvalidListingTypeError);
        }

        if (!Enum.IsDefined(request.PropertyType))
        {
            return Failure("propertyType", InvalidPropertyTypeError);
        }

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

        if (request.Translations is null || request.Translations.Count == 0)
        {
            return Failure("translations", "At least one translation is required.");
        }

        ValidationFailure? translationFailure =
            ValidateTranslations(request.Translations);

        if (translationFailure is not null)
        {
            return translationFailure;
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

    private static ValidationFailure? ValidateTranslations(
        IReadOnlyList<CreateListingTranslationRequest> translations)
    {
        var normalizedLanguages = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < translations.Count; index++)
        {
            CreateListingTranslationRequest translation = translations[index];
            string languageKey = $"translations[{index}].languageCode";

            if (translation.LanguageCode is null)
            {
                return Failure(
                    languageKey,
                    "Translation language code is required.");
            }

            string normalizedLanguage =
                ListingTranslationRules.NormalizeLanguageCode(
                    translation.LanguageCode);

            if (normalizedLanguage.Length == 0)
            {
                return Failure(
                    languageKey,
                    "Translation language code is required.");
            }

            if (normalizedLanguage.Length >
                ListingTranslationRules.LanguageCodeMaxLength)
            {
                return Failure(
                    languageKey,
                    $"Translation language code cannot exceed {ListingTranslationRules.LanguageCodeMaxLength} characters.");
            }

            if (!ListingTranslationRules.IsCanonicalLanguageCode(
                    normalizedLanguage))
            {
                return Failure(languageKey, InvalidLanguageCodeError);
            }

            if (!normalizedLanguages.Add(normalizedLanguage))
            {
                return Failure(
                    "translations",
                    "Duplicate translation languages are not allowed.");
            }

            string titleKey = $"translations[{index}].title";

            if (translation.Title is null)
            {
                return Failure(
                    titleKey,
                    "Translation title is required.");
            }

            string normalizedTitle =
                ListingTranslationRules.NormalizeRequiredText(
                    translation.Title);

            if (normalizedTitle.Length == 0)
            {
                return Failure(
                    titleKey,
                    "Translation title is required.");
            }

            if (normalizedTitle.Length > ListingTranslationRules.TitleMaxLength)
            {
                return Failure(
                    titleKey,
                    $"Translation title cannot exceed {ListingTranslationRules.TitleMaxLength} characters.");
            }

            ValidationFailure? optionalTextFailure =
                ValidateOptionalText(
                    translation.Description,
                    $"translations[{index}].description",
                    "Translation description",
                    ListingTranslationRules.DescriptionMaxLength)
                ?? ValidateOptionalText(
                    translation.AddressLine,
                    $"translations[{index}].addressLine",
                    "Translation address",
                    ListingTranslationRules.AddressLineMaxLength)
                ?? ValidateOptionalText(
                    translation.City,
                    $"translations[{index}].city",
                    "Translation city",
                    ListingTranslationRules.LocationMaxLength)
                ?? ValidateOptionalText(
                    translation.Municipality,
                    $"translations[{index}].municipality",
                    "Translation municipality",
                    ListingTranslationRules.LocationMaxLength)
                ?? ValidateOptionalText(
                    translation.Neighborhood,
                    $"translations[{index}].neighborhood",
                    "Translation neighborhood",
                    ListingTranslationRules.LocationMaxLength);

            if (optionalTextFailure is not null)
            {
                return optionalTextFailure;
            }
        }

        return null;
    }

    private static ValidationFailure? ValidateOptionalText(
        string? value,
        string key,
        string fieldName,
        int maxLength)
    {
        string? normalized =
            ListingTranslationRules.NormalizeOptionalText(value);

        return normalized?.Length > maxLength
            ? Failure(
                key,
                $"{fieldName} cannot exceed {maxLength} characters.")
            : null;
    }
}
