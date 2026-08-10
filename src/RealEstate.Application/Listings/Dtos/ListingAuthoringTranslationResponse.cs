namespace RealEstate.Application.Listings.Dtos;

public sealed class ListingAuthoringTranslationResponse
{
    public Guid Id { get; set; }

    public required string LanguageCode { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Municipality { get; set; }

    public string? Neighborhood { get; set; }
}
