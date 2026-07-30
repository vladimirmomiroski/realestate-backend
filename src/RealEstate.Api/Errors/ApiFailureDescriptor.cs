using RealEstate.Application.Common;

namespace RealEstate.Api.Errors;

internal sealed record ApiFailureDescriptor(
    int StatusCode,
    string Code,
    string Title,
    string Detail)
{
    public static readonly ApiFailureDescriptor ValidationFailed = new(
        StatusCodes.Status400BadRequest,
        ErrorCodes.ValidationFailed,
        "Validation failed",
        "One or more validation errors occurred.");

    public static readonly ApiFailureDescriptor AuthenticationRequired = new(
        StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthenticationRequired,
        "Authentication required",
        "Authentication is required to access this resource.");

    public static readonly ApiFailureDescriptor AuthenticationInvalidCredentials = new(
        StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthenticationInvalidCredentials,
        "Invalid credentials",
        "The email or password is invalid.");

    public static readonly ApiFailureDescriptor AuthenticationInvalidPrincipal = new(
        StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthenticationInvalidPrincipal,
        "Invalid authenticated principal",
        "The authenticated user could not be resolved.");

    public static readonly ApiFailureDescriptor AuthorizationForbidden = new(
        StatusCodes.Status403Forbidden,
        ErrorCodes.AuthorizationForbidden,
        "Forbidden",
        "You do not have permission to perform this action.");

    public static readonly ApiFailureDescriptor AuthorizationAccountDisabled = new(
        StatusCodes.Status403Forbidden,
        ErrorCodes.AuthorizationAccountDisabled,
        "Account disabled",
        "This account cannot perform this action.");

    public static readonly ApiFailureDescriptor ResourceNotFound = new(
        StatusCodes.Status404NotFound,
        ErrorCodes.ResourceNotFound,
        "Resource not found",
        "The requested resource was not found.");

    public static readonly ApiFailureDescriptor MethodNotAllowed = new(
        StatusCodes.Status405MethodNotAllowed,
        ErrorCodes.RequestMethodNotAllowed,
        "Method not allowed",
        "The requested HTTP method is not supported for this resource.");

    public static readonly ApiFailureDescriptor ResourceStateConflict = new(
        StatusCodes.Status409Conflict,
        ErrorCodes.ConflictResourceState,
        "Conflict",
        "The request conflicts with the current resource state.");

    public static readonly ApiFailureDescriptor ResourceCapacityConflict = new(
        StatusCodes.Status409Conflict,
        ErrorCodes.ConflictResourceCapacity,
        "Conflict",
        "The resource has reached its allowed capacity.");

    public static readonly ApiFailureDescriptor ResourceSetChangedConflict = new(
        StatusCodes.Status409Conflict,
        ErrorCodes.ConflictResourceSetChanged,
        "Conflict",
        "The submitted resource set no longer matches the current resource.");

    public static readonly ApiFailureDescriptor EmailAlreadyExists = new(
        StatusCodes.Status409Conflict,
        ErrorCodes.ConflictEmailAlreadyExists,
        "Email already exists",
        "An account with this email already exists.");

    public static readonly ApiFailureDescriptor MediaTypeNotSupported = new(
        StatusCodes.Status415UnsupportedMediaType,
        ErrorCodes.RequestMediaTypeNotSupported,
        "Unsupported media type",
        "The request media type is not supported.");

    public static readonly ApiFailureDescriptor Unexpected = new(
        StatusCodes.Status500InternalServerError,
        ErrorCodes.ServerUnexpected,
        "Unexpected server error",
        "An unexpected error occurred.");

    public static ApiFailureDescriptor ForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => AuthenticationRequired,
            StatusCodes.Status403Forbidden => AuthorizationForbidden,
            StatusCodes.Status404NotFound => ResourceNotFound,
            StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
            StatusCodes.Status409Conflict => ResourceStateConflict,
            StatusCodes.Status415UnsupportedMediaType => MediaTypeNotSupported,
            StatusCodes.Status500InternalServerError => Unexpected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                statusCode,
                "The status code has no canonical API failure descriptor.")
        };
    }

    public static ApiFailureDescriptor ForCode(string errorCode)
    {
        return errorCode switch
        {
            ErrorCodes.AuthenticationRequired => AuthenticationRequired,
            ErrorCodes.AuthenticationInvalidCredentials =>
                AuthenticationInvalidCredentials,
            ErrorCodes.AuthenticationInvalidPrincipal =>
                AuthenticationInvalidPrincipal,
            ErrorCodes.AuthorizationForbidden => AuthorizationForbidden,
            ErrorCodes.AuthorizationAccountDisabled =>
                AuthorizationAccountDisabled,
            ErrorCodes.ResourceNotFound => ResourceNotFound,
            ErrorCodes.RequestMethodNotAllowed => MethodNotAllowed,
            ErrorCodes.RequestMediaTypeNotSupported => MediaTypeNotSupported,
            ErrorCodes.ConflictEmailAlreadyExists => EmailAlreadyExists,
            ErrorCodes.ConflictResourceState => ResourceStateConflict,
            ErrorCodes.ConflictResourceCapacity => ResourceCapacityConflict,
            ErrorCodes.ConflictResourceSetChanged => ResourceSetChangedConflict,
            ErrorCodes.ServerUnexpected => Unexpected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(errorCode),
                errorCode,
                "The error code has no canonical API failure descriptor.")
        };
    }
}
