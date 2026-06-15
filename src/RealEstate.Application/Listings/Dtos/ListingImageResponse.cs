namespace RealEstate.Application.Listings.Dtos;

public sealed class ListingImageResponse
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }
}
