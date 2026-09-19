using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;

public sealed class GetAgencyDashboardListingsValidator
{
    public const string UndefinedStatusError =
        "Status must be a defined value.";

    public string? Validate(GetAgencyDashboardListingsQuery query)
    {
        return query.Status.HasValue &&
               !Enum.IsDefined(query.Status.Value)
            ? UndefinedStatusError
            : null;
    }
}
