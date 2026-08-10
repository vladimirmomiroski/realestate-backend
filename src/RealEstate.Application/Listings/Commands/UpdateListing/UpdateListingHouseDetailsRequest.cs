using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingHouseDetailsRequest
{
    public HouseType HouseType { get; set; } = HouseType.Unknown;

    public int? NumberOfFloors { get; set; }

    public decimal? YardAreaSquareMeters { get; set; }
}
