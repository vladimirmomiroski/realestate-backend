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

        if (request.Translations.Count == 0)
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

        return null;
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        return languageCode.Trim().ToLowerInvariant();
    }
}
