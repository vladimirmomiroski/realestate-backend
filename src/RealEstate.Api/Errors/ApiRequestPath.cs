namespace RealEstate.Api.Errors;

internal static class ApiRequestPath
{
    private static readonly PathString ApiRoot = new("/api");
    private static readonly PathString UploadsRoot = new("/uploads");

    public static bool IsApi(PathString path)
    {
        return path.StartsWithSegments(
            ApiRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool NeedsRequestIdentifier(PathString path)
    {
        return IsApi(path) || path.StartsWithSegments(
            UploadsRoot,
            StringComparison.OrdinalIgnoreCase);
    }

}
