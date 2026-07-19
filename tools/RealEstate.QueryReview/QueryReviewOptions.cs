namespace RealEstate.QueryReview;

internal enum QueryReviewCommand
{
    Doctor,
    ProfileCreate,
    ProfileVerify,
    CaptureSql
}

internal sealed record QueryReviewOptions(
    QueryReviewCommand Command,
    string ConnectionString,
    bool ConfirmDisposable,
    string OutputDirectory)
{
    private const string ConnectionStringOption = "--connection-string";
    private const string ConfirmDisposableOption = "--confirm-disposable";

    public static string Usage =>
        "Usage:\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- doctor " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- profile create " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- profile verify " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- capture-sql " +
        "--connection-string \"<connection-string>\" --confirm-disposable";

    public static bool TryParse(
        string[] args,
        out QueryReviewOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        if (!TryParseCommand(args, out var command, out var optionsStartIndex, out error))
        {
            return false;
        }

        string? connectionString = null;
        var confirmDisposable = false;

        for (var index = optionsStartIndex; index < args.Length; index++)
        {
            switch (args[index])
            {
                case ConnectionStringOption:
                    if (connectionString is not null)
                    {
                        error = $"Option '{ConnectionStringOption}' may be supplied only once.";
                        return false;
                    }

                    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = $"Option '{ConnectionStringOption}' requires a value.";
                        return false;
                    }

                    connectionString = args[++index];
                    break;

                case ConfirmDisposableOption:
                    if (confirmDisposable)
                    {
                        error = $"Option '{ConfirmDisposableOption}' may be supplied only once.";
                        return false;
                    }

                    confirmDisposable = true;
                    break;

                default:
                    error = $"Unknown option '{args[index]}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = $"An explicit '{ConnectionStringOption}' value is required.";
            return false;
        }

        if (!confirmDisposable)
        {
            error = $"The safety acknowledgement '{ConfirmDisposableOption}' is required.";
            return false;
        }

        var outputDirectory = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "realestate-queryreview"));

        options = new QueryReviewOptions(
            command,
            connectionString,
            confirmDisposable,
            outputDirectory);

        return true;
    }

    private static bool TryParseCommand(
        string[] args,
        out QueryReviewCommand command,
        out int optionsStartIndex,
        out string? error)
    {
        command = default;
        optionsStartIndex = 0;
        error = null;

        if (args.Length > 0 && string.Equals(args[0], "doctor", StringComparison.Ordinal))
        {
            command = QueryReviewCommand.Doctor;
            optionsStartIndex = 1;
            return true;
        }

        if (args.Length > 1 && string.Equals(args[0], "profile", StringComparison.Ordinal))
        {
            if (string.Equals(args[1], "create", StringComparison.Ordinal))
            {
                command = QueryReviewCommand.ProfileCreate;
                optionsStartIndex = 2;
                return true;
            }

            if (string.Equals(args[1], "verify", StringComparison.Ordinal))
            {
                command = QueryReviewCommand.ProfileVerify;
                optionsStartIndex = 2;
                return true;
            }
        }

        if (args.Length > 0 && string.Equals(args[0], "capture-sql", StringComparison.Ordinal))
        {
            command = QueryReviewCommand.CaptureSql;
            optionsStartIndex = 1;
            return true;
        }

        error =
            "Supported commands are 'doctor', 'profile create', 'profile verify', and " +
            "'capture-sql'.";
        return false;
    }
}
