using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingValidator
{
    public string? Validate(CreateListingRequest request)
    {
        if (request.Price <= 0)
        {
            return "Price must be greater than zero.";
        }

        if (request.AreaSquareMeters <= 0)
        {
            return "Area must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return "Currency is required.";
        }

        if (request.Translations is null || request.Translations.Count == 0)
        {
            return "At least one translation is required.";
        }

        if (request.Translations.Any(translation => string.IsNullOrWhiteSpace(translation.LanguageCode)))
        {
            return "Translation language code is required.";
        }

        if (request.Translations.Any(translation => string.IsNullOrWhiteSpace(translation.Title)))
        {
            return "Translation title is required.";
        }

        var hasDuplicateLanguages = request.Translations
            .GroupBy(translation => NormalizeLanguageCode(translation.LanguageCode))
            .Any(group => group.Count() > 1);

        if (hasDuplicateLanguages)
        {
            return "Duplicate translation languages are not allowed.";
        }

        if (request.BalconyCount is < 0)
        {
            return "Balcony count cannot be negative.";
        }

        if (request.ParkingSpaces is < 0)
        {
            return "Parking spaces cannot be negative.";
        }

        if (request.YearRenovated is < 1800 or > 2100)
        {
            return "Year renovated is not valid.";
        }

        if (request.YearRenovated.HasValue &&
            request.YearBuilt.HasValue &&
            request.YearRenovated.Value < request.YearBuilt.Value)
        {
            return "Year renovated cannot be earlier than year built.";
        }

        if (request.PropertyType == PropertyType.Apartment)
        {
            if (request.ApartmentDetails is null)
            {
                return "Apartment details are required for apartment listings.";
            }

            if (request.HouseDetails is not null)
            {
                return "House details are not allowed for apartment listings.";
            }

            if (request.ApartmentDetails.Floor is < 0)
            {
                return "Floor cannot be negative.";
            }

            if (request.ApartmentDetails.TotalFloors is < 0)
            {
                return "Total floors cannot be negative.";
            }

            if (request.ApartmentDetails.Floor.HasValue &&
                request.ApartmentDetails.TotalFloors.HasValue &&
                request.ApartmentDetails.Floor.Value > request.ApartmentDetails.TotalFloors.Value)
            {
                return "Floor cannot be greater than total floors.";
            }
        }

        if (request.PropertyType == PropertyType.House)
        {
            if (request.HouseDetails is null)
            {
                return "House details are required for house listings.";
            }

            if (request.ApartmentDetails is not null)
            {
                return "Apartment details are not allowed for house listings.";
            }

            if (request.HouseDetails.NumberOfFloors is < 0)
            {
                return "Number of floors cannot be negative.";
            }

            if (request.HouseDetails.YardAreaSquareMeters is < 0)
            {
                return "Yard area cannot be negative.";
            }
        }

        if (request.AgencyId == Guid.Empty)
        {
            return "Agency id cannot be empty.";
        }


        return null;
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        return languageCode.Trim().ToLowerInvariant();
    }
}
