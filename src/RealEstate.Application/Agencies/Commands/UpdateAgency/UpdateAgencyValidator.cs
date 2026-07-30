using RealEstate.Application.Agencies.Dtos;

namespace RealEstate.Application.Agencies.Commands.UpdateAgency;

public sealed class UpdateAgencyValidator
{
    public sealed record ValidationFailure(string Key, string Error);

    public string? Validate(UpdateAgencyRequest request)
    {
        return ValidateWithKey(request)?.Error;
    }

    public ValidationFailure? ValidateWithKey(UpdateAgencyRequest request)
    {
        if (request is null)
        {
            return Failure("request", "Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("name", "Agency name is required.");
        }

        if (request.Name.Trim().Length > 150)
        {
            return Failure("name", "Agency name cannot be longer than 150 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Description) &&
            request.Description.Trim().Length > 1000)
        {
            return Failure("description", "Agency description cannot be longer than 1000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            request.PhoneNumber.Trim().Length > 50)
        {
            return Failure("phoneNumber", "Agency phone number cannot be longer than 50 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            request.Email.Trim().Length > 254)
        {
            return Failure("email", "Agency email cannot be longer than 254 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !request.Email.Contains('@'))
        {
            return Failure("email", "Agency email is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(request.WebsiteUrl) &&
            request.WebsiteUrl.Trim().Length > 500)
        {
            return Failure("websiteUrl", "Agency website url cannot be longer than 500 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.WebsiteUrl) &&
            !Uri.TryCreate(request.WebsiteUrl.Trim(), UriKind.Absolute, out _))
        {
            return Failure("websiteUrl", "Agency website url is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(request.AddressLine) &&
            request.AddressLine.Trim().Length > 250)
        {
            return Failure("addressLine", "Agency address line cannot be longer than 250 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.City) &&
            request.City.Trim().Length > 100)
        {
            return Failure("city", "Agency city cannot be longer than 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Municipality) &&
            request.Municipality.Trim().Length > 100)
        {
            return Failure("municipality", "Agency municipality cannot be longer than 100 characters.");
        }

        return null;
    }

    private static ValidationFailure Failure(string key, string error)
    {
        return new ValidationFailure(key, error);
    }
}
