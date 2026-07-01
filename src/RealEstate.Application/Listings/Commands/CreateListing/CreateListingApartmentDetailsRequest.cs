using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingApartmentDetailsRequest
{
    public ApartmentType ApartmentType { get; set; } = ApartmentType.Unknown;

    public int? Floor { get; set; }

    public int? TotalFloors { get; set; }

    public bool? HasElevator { get; set; }
}