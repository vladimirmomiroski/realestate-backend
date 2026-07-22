namespace RealEstate.Application.Listings.Queries.GetListings;

public enum ListingSortOption
{
    Newest = 1,
    PriceAsc = 2,
    PriceDesc = 3
}

public static class ListingSortOptionParser
{
    public static bool TryParse(
        string? value,
        out ListingSortOption sortOption)
    {
        sortOption = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedValue = value.Trim();

        if (normalizedValue.Equals(
            "newest",
            StringComparison.OrdinalIgnoreCase))
        {
            sortOption = ListingSortOption.Newest;
            return true;
        }

        if (normalizedValue.Equals(
            "priceAsc",
            StringComparison.OrdinalIgnoreCase))
        {
            sortOption = ListingSortOption.PriceAsc;
            return true;
        }

        if (normalizedValue.Equals(
            "priceDesc",
            StringComparison.OrdinalIgnoreCase))
        {
            sortOption = ListingSortOption.PriceDesc;
            return true;
        }

        return false;
    }
}
