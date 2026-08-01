using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.ChangeAgencyMemberRole;

public sealed class ChangeAgencyMemberRoleValidator
{
    public sealed record ValidationFailure(string Key, string Error);

    public string? Validate(ChangeAgencyMemberRoleRequest? request)
    {
        return ValidateWithKey(request)?.Error;
    }

    public ValidationFailure? ValidateWithKey(
        ChangeAgencyMemberRoleRequest? request)
    {
        if (request is null)
        {
            return Failure("request", "Request is required.");
        }

        if (request.Role is not AgencyMemberRole.Owner
            and not AgencyMemberRole.Agent)
        {
            return Failure(
                "role",
                "Agency member role must be Owner or Agent.");
        }

        return null;
    }

    private static ValidationFailure Failure(
        string key,
        string error)
    {
        return new ValidationFailure(key, error);
    }
}
