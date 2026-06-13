namespace RealEstate.Application.Common;

public enum ServiceResultStatus
{
    Success = 1,
    ValidationError = 2,
    NotFound = 3
}

public sealed record ServiceResult<T>(
    ServiceResultStatus Status,
    T? Value = default,
    string? Error = null)
{
    public static ServiceResult<T> Success(T value)
    {
        return new ServiceResult<T>(ServiceResultStatus.Success, value);
    }

    public static ServiceResult<T> ValidationError(string error)
    {
        return new ServiceResult<T>(ServiceResultStatus.ValidationError, default, error);
    }

    public static ServiceResult<T> NotFound(string error)
    {
        return new ServiceResult<T>(ServiceResultStatus.NotFound, default, error);
    }
}
