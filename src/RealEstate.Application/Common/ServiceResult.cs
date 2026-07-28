namespace RealEstate.Application.Common;

public enum ServiceResultStatus
{
    Success = 1,
    ValidationError = 2,
    NotFound = 3,
    Forbidden = 4,
    Unauthorized = 5,
    Conflict = 6
}

public sealed record ServiceResult<T>(
    ServiceResultStatus Status,
    T? Value = default,
    string? Error = null)
{
    public string? ErrorCode { get; private init; }

    public static ServiceResult<T> Success(T value)
    {
        return new ServiceResult<T>(ServiceResultStatus.Success, value);
    }

    public static ServiceResult<T> ValidationError(string error)
    {
        return new ServiceResult<T>(ServiceResultStatus.ValidationError, default, error);
    }

    public static ServiceResult<T> ValidationError(
        string error,
        string errorCode)
    {
        return Failure(ServiceResultStatus.ValidationError, error, errorCode);
    }

    public static ServiceResult<T> NotFound(string error)
    {
        return new ServiceResult<T>(ServiceResultStatus.NotFound, default, error);
    }

    public static ServiceResult<T> NotFound(string error, string errorCode)
    {
        return Failure(ServiceResultStatus.NotFound, error, errorCode);
    }

    public static ServiceResult<T> Forbidden(string error)
    {
        return new ServiceResult<T>(ServiceResultStatus.Forbidden, default, error);
    }

    public static ServiceResult<T> Forbidden(string error, string errorCode)
    {
        return Failure(ServiceResultStatus.Forbidden, error, errorCode);
    }

    public static ServiceResult<T> Unauthorized(string error)
    {
        return new ServiceResult<T>(ServiceResultStatus.Unauthorized, default, error);
    }

    public static ServiceResult<T> Unauthorized(
        string error,
        string errorCode)
    {
        return Failure(ServiceResultStatus.Unauthorized, error, errorCode);
    }

    public static ServiceResult<T> Conflict(string error, string errorCode)
    {
        return Failure(ServiceResultStatus.Conflict, error, errorCode);
    }

    private static ServiceResult<T> Failure(
        ServiceResultStatus status,
        string error,
        string errorCode)
    {
        if (!ErrorCodes.IsDefined(errorCode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorCode),
                errorCode,
                "The error code is not in the closed error catalogue.");
        }

        return new ServiceResult<T>(status, default, error)
        {
            ErrorCode = errorCode
        };
    }
}
