namespace RealEstate.Application.Agencies.Commands.AcceptAgencyInvitation;

public sealed class AcceptAgencyInvitationValidator
{
    public sealed record ValidationFailure(string Key, string Error);

    public string? Validate(AcceptAgencyInvitationRequest? request)
    {
        return ValidateWithKey(request)?.Error;
    }

    public ValidationFailure? ValidateWithKey(
        AcceptAgencyInvitationRequest? request)
    {
        if (request is null)
        {
            return Failure("request", "Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Failure(
                "token",
                "Invitation token is required.");
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
