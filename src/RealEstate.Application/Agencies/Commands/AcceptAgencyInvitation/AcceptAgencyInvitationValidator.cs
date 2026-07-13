namespace RealEstate.Application.Agencies.Commands.AcceptAgencyInvitation;

public sealed class AcceptAgencyInvitationValidator
{
    public string? Validate(AcceptAgencyInvitationRequest? request)
    {
        if (request is null)
        {
            return "Request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return "Invitation token is required.";
        }

        return null;
    }
}
