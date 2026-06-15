using RealEstate.Domain.Common;

namespace RealEstate.Domain.Entities;

public class ListingImage : IAuditableEntity
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }

    public Listing Listing { get; set; } = null!;
}