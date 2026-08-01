using System.Collections.Frozen;

namespace RealEstate.Application.Common;

public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string ValidationFileRequired = "validation.file_required";
    public const string ValidationFileEmpty = "validation.file_empty";
    public const string ValidationFileTooLarge = "validation.file_too_large";
    public const string ValidationFileTypeNotSupported =
        "validation.file_type_not_supported";

    public const string AuthenticationRequired = "authentication.required";
    public const string AuthenticationInvalidCredentials =
        "authentication.invalid_credentials";
    public const string AuthenticationInvalidPrincipal =
        "authentication.invalid_principal";

    public const string AuthorizationForbidden = "authorization.forbidden";
    public const string AuthorizationAccountDisabled =
        "authorization.account_disabled";

    public const string ResourceNotFound = "resource.not_found";

    public const string RequestMethodNotAllowed =
        "request.method_not_allowed";
    public const string RequestMediaTypeNotSupported =
        "request.media_type_not_supported";

    public const string ConflictEmailAlreadyExists =
        "conflict.email_already_exists";
    public const string ConflictAgencySlugAlreadyExists =
        "conflict.agency_slug_already_exists";
    public const string ConflictResourceState = "conflict.resource_state";
    public const string ConflictResourceCapacity =
        "conflict.resource_capacity";
    public const string ConflictResourceSetChanged =
        "conflict.resource_set_changed";

    public const string ServerUnexpected = "server.unexpected";

    private static readonly FrozenSet<string> DefinedCodes = new[]
    {
        ValidationFailed,
        ValidationFileRequired,
        ValidationFileEmpty,
        ValidationFileTooLarge,
        ValidationFileTypeNotSupported,
        AuthenticationRequired,
        AuthenticationInvalidCredentials,
        AuthenticationInvalidPrincipal,
        AuthorizationForbidden,
        AuthorizationAccountDisabled,
        ResourceNotFound,
        RequestMethodNotAllowed,
        RequestMediaTypeNotSupported,
        ConflictEmailAlreadyExists,
        ConflictAgencySlugAlreadyExists,
        ConflictResourceState,
        ConflictResourceCapacity,
        ConflictResourceSetChanged,
        ServerUnexpected
    }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedCodes;

    public static bool IsDefined(string? code)
    {
        return code is not null && DefinedCodes.Contains(code);
    }
}
