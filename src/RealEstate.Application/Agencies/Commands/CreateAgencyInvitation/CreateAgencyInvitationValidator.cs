using System.Net.Mail;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;

public sealed class CreateAgencyInvitationValidator
{
    public string? Validate(CreateAgencyInvitationRequest? request)
    {
        if (request is null)
        {
            return "Request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Invitation email is required.";
        }

        string email = request.Email.Trim();

        if (email.Length > 254)
        {
            return "Invitation email cannot be longer than 254 characters.";
        }

        if (!MailAddress.TryCreate(email, out MailAddress? mailAddress) ||
            mailAddress.Address != email)
        {
            return "Invitation email is invalid.";
        }

        if (request.Role is not AgencyMemberRole.Owner and not AgencyMemberRole.Agent)
        {
            return "Invitation role must be Owner or Agent.";
        }

        return null;
    }
}
