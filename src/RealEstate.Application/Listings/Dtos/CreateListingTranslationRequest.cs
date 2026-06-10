namespace RealEstate.Application.Listings.Dtos;

public sealed class CreateListingTranslationRequest
{
    public string LanguageCode { get; set; } = default!;

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Neighborhood { get; set; }
}