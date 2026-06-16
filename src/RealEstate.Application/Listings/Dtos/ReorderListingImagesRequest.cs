namespace RealEstate.Application.Listings.Dtos;

public sealed class ReorderListingImagesRequest
{
    public List<Guid> ImageIds { get; set; } = new List<Guid>();
}
