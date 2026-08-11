using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Domain.Entities;

internal static class ListingStatusTestMaterializer
{
    public static void MaterializeUnreachableStatus(
        Listing listing,
        ListingStatus status)
    {
        ArgumentNullException.ThrowIfNull(listing);

        if (status is not ListingStatus.Reserved and
            not ListingStatus.Sold and
            not ListingStatus.Rented)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Only lifecycle states without a current Domain transition may be materialized.");
        }

        typeof(Listing)
            .GetProperty(nameof(Listing.Status))!
            .SetValue(listing, status);
    }
}
