using System.Net.Mail;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;

public sealed class CreateAgencyInvitationValidator
{
    public sealed record ValidationFailure(string Key, string Error);

    public string? Validate(CreateAgencyInvitationRequest? request)
    {
        return ValidateWithKey(request)?.Error;
    }

    public ValidationFailure? ValidateWithKey(
        CreateAgencyInvitationRequest? request)
    {
        if (request is null)
        {
            return Failure("request", "Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Failure(
                "email",
                "Invitation email is required.");
        }

        string email = request.Email.Trim();

        if (email.Length > 254)
        {
            return Failure(
                "email",
                "Invitation email cannot be longer than 254 characters.");
        }

        if (!MailAddress.TryCreate(email, out MailAddress? mailAddress) ||
            mailAddress.Address != email)
        {
            return Failure(
                "email",
                "Invitation email is invalid.");
        }

        if (request.Role is not AgencyMemberRole.Owner and not AgencyMemberRole.Agent)
        {
            return Failure(
                "role",
                "Invitation role must be Owner or Agent.");
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
