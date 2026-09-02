using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingApartmentDetailsRequest
{
    public ApartmentType ApartmentType { get; set; } = ApartmentType.Unknown;

    public int? Floor { get; set; }

    public int? TotalFloors { get; set; }

    public bool? HasElevator { get; set; }
}
