using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Dtos;

public sealed class ListingApartmentDetailsResponse
{
    public ApartmentType ApartmentType { get; set; }

    public int? Floor { get; set; }

    public int? TotalFloors { get; set; }

    public bool? HasElevator { get; set; }
}
