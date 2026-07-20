namespace RealEstate.QueryReview;

internal enum QueryReviewCommand
{
    Doctor,
    ProfileCreate,
    ProfileVerify,
    CaptureSql,
    BaselineRun,
    BaselineVerify
}

internal sealed record QueryReviewOptions(
    QueryReviewCommand Command,
    string? ConnectionString,
    bool ConfirmDisposable,
    string OutputDirectory,
    string? ContainerName,
    string? RunDirectory)
{
    private const string ConnectionStringOption = "--connection-string";
    private const string ConfirmDisposableOption = "--confirm-disposable";
    private const string ContainerNameOption = "--container-name";
    private const string RunDirectoryOption = "--run-directory";

    public static string Usage =>
        "Usage:\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- doctor " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- profile create " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- profile verify " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- capture-sql " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- baseline run " +
        "--connection-string \"<connection-string>\" --confirm-disposable " +
        "--container-name <container-name>\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- baseline verify " +
        "--run-directory \"<absolute-raw-run-directory>\"";

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
        string? containerName = null;
        string? runDirectory = null;

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

                case ContainerNameOption:
                    if (containerName is not null)
                    {
                        error = $"Option '{ContainerNameOption}' may be supplied only once.";
                        return false;
                    }

                    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = $"Option '{ContainerNameOption}' requires a value.";
                        return false;
                    }

                    containerName = args[++index].Trim();
                    break;

                case RunDirectoryOption:
                    if (runDirectory is not null)
                    {
                        error = $"Option '{RunDirectoryOption}' may be supplied only once.";
                        return false;
                    }

                    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = $"Option '{RunDirectoryOption}' requires a value.";
                        return false;
                    }

                    runDirectory = args[++index].Trim();
                    break;

                default:
                    error = $"Unknown option '{args[index]}'.";
                    return false;
            }
        }

        if (command == QueryReviewCommand.BaselineVerify)
        {
            if (connectionString is not null || confirmDisposable || containerName is not null)
            {
                error =
                    "'baseline verify' is offline and rejects connection, disposable, and " +
                    "container options.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(runDirectory))
            {
                error = $"The offline baseline verifier requires '{RunDirectoryOption}'.";
                return false;
            }

            if (!Path.IsPathFullyQualified(runDirectory))
            {
                error = $"Option '{RunDirectoryOption}' must be an absolute path.";
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = $"An explicit '{ConnectionStringOption}' value is required.";
            return false;
        }

        if (command != QueryReviewCommand.BaselineVerify && !confirmDisposable)
        {
            error = $"The safety acknowledgement '{ConfirmDisposableOption}' is required.";
            return false;
        }

        if (command == QueryReviewCommand.BaselineRun && string.IsNullOrWhiteSpace(containerName))
        {
            error = $"The official baseline command requires '{ContainerNameOption}'.";
            return false;
        }

        if (command != QueryReviewCommand.BaselineRun && containerName is not null)
        {
            error = $"Option '{ContainerNameOption}' is valid only for 'baseline run'.";
            return false;
        }

        if (command != QueryReviewCommand.BaselineVerify && runDirectory is not null)
        {
            error = $"Option '{RunDirectoryOption}' is valid only for 'baseline verify'.";
            return false;
        }

        var outputDirectory = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "realestate-queryreview"));

        options = new QueryReviewOptions(
            command,
            connectionString,
            confirmDisposable,
            outputDirectory,
            containerName,
            runDirectory is null ? null : Path.GetFullPath(runDirectory));

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

        if (args.Length > 1 &&
            string.Equals(args[0], "baseline", StringComparison.Ordinal) &&
            string.Equals(args[1], "run", StringComparison.Ordinal))
        {
            command = QueryReviewCommand.BaselineRun;
            optionsStartIndex = 2;
            return true;
        }

        if (args.Length > 1 &&
            string.Equals(args[0], "baseline", StringComparison.Ordinal) &&
            string.Equals(args[1], "verify", StringComparison.Ordinal))
        {
            command = QueryReviewCommand.BaselineVerify;
            optionsStartIndex = 2;
            return true;
        }

        error =
            "Supported commands are 'doctor', 'profile create', 'profile verify', and " +
            "'capture-sql', 'baseline run', and 'baseline verify'.";
        return false;
    }
}
