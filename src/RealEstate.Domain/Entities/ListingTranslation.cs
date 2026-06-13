namespace RealEstate.Domain.Entities;

public class ListingTranslation
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public string LanguageCode { get; set; } = default!;

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Neighborhood { get; set; }

    public Listing Listing { get; set; } = default!;
}