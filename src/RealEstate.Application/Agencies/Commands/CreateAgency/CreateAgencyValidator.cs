using System.Text.RegularExpressions;
using RealEstate.Application.Agencies.Dtos;

namespace RealEstate.Application.Agencies.Commands.CreateAgency;

public sealed class CreateAgencyValidator
{
    private static readonly Regex SlugRegex = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled);

    public string? Validate(CreateAgencyRequest request)
    {
        if (request is null)
        {
            return "Request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Agency name is required.";
        }

        if (request.Name.Trim().Length > 150)
        {
            return "Agency name cannot be longer than 150 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            return "Agency slug is required.";
        }

        string slug = NormalizeSlug(request.Slug);

        if (slug.Length < 3)
        {
            return "Agency slug must be at least 3 characters long.";
        }

        if (slug.Length > 100)
        {
            return "Agency slug cannot be longer than 100 characters.";
        }

        if (!SlugRegex.IsMatch(slug))
        {
            return "Agency slug can contain only lowercase letters, numbers, and hyphens.";
        }

        if (!string.IsNullOrWhiteSpace(request.Description) &&
            request.Description.Trim().Length > 1000)
        {
            return "Agency description cannot be longer than 1000 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            request.PhoneNumber.Trim().Length > 50)
        {
            return "Agency phone number cannot be longer than 50 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            request.Email.Trim().Length > 254)
        {
            return "Agency email cannot be longer than 254 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !request.Email.Contains('@'))
        {
            return "Agency email is invalid.";
        }

        if (!string.IsNullOrWhiteSpace(request.WebsiteUrl) &&
            request.WebsiteUrl.Trim().Length > 500)
        {
            return "Agency website url cannot be longer than 500 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.WebsiteUrl) &&
            !Uri.TryCreate(request.WebsiteUrl.Trim(), UriKind.Absolute, out _))
        {
            return "Agency website url is invalid.";
        }

        if (!string.IsNullOrWhiteSpace(request.AddressLine) &&
            request.AddressLine.Trim().Length > 250)
        {
            return "Agency address line cannot be longer than 250 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.City) &&
            request.City.Trim().Length > 100)
        {
            return "Agency city cannot be longer than 100 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.Municipality) &&
            request.Municipality.Trim().Length > 100)
        {
            return "Agency municipality cannot be longer than 100 characters.";
        }

        return null;
    }

    private static string NormalizeSlug(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }
}
