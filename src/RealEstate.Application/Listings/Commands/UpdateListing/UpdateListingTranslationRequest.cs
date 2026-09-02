namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingTranslationRequest
{
    public required string LanguageCode { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Municipality { get; set; }

    public string? Neighborhood { get; set; }
}
