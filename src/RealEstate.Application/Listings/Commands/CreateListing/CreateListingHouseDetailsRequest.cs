using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingHouseDetailsRequest
{
    public HouseType HouseType { get; set; } = HouseType.Unknown;

    public int? NumberOfFloors { get; set; }

    public decimal? YardAreaSquareMeters { get; set; }
}