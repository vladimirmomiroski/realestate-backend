using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Dtos;

public sealed class ListingHouseDetailsResponse
{
    public HouseType HouseType { get; set; }

    public int? NumberOfFloors { get; set; }

    public decimal? YardAreaSquareMeters { get; set; }
}
