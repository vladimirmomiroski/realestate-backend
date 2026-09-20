namespace RealEstate.QueryReview;

internal enum QueryReviewCommand
{
    Doctor,
    ProfileCreate,
    ProfileVerify,
    CaptureSql,
    BaselineRun,
    BaselineVerify,
    BaselineExport
}

internal sealed record QueryReviewOptions(
    QueryReviewCommand Command,
    string? Profile,
    string? ConnectionString,
    bool ConfirmDisposable,
    string OutputDirectory,
    string? ContainerName,
    string? RunDirectory,
    string? ComparisonRunDirectory,
    bool ConfirmEvidenceExport)
{
    private const string ProfileOption = "--profile";
    private const string ConnectionStringOption = "--connection-string";
    private const string ConfirmDisposableOption = "--confirm-disposable";
    private const string ContainerNameOption = "--container-name";
    private const string RunDirectoryOption = "--run-directory";
    private const string RunDirectoryAlias = "--run-dir";
    private const string ComparisonRunDirectoryOption = "--comparison-run-dir";
    private const string ConfirmEvidenceExportOption = "--confirm-evidence-export";

    public static string Usage =>
        "Usage:\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- doctor " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- profile create " +
        "--profile <generation> " +
        "--connection-string \"<connection-string>\" --confirm-disposable " +
        "--container-name <container-name>\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- profile verify " +
        "--profile <generation> " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- capture-sql " +
        "--profile <generation> " +
        "--connection-string \"<connection-string>\" --confirm-disposable\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- baseline run " +
        "--profile <generation> " +
        "--connection-string \"<connection-string>\" --confirm-disposable " +
        "--container-name <container-name>\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- baseline verify " +
        "[--profile <generation>] --run-dir \"<artifact-directory>\"\n" +
        "  dotnet run --project tools/RealEstate.QueryReview -- baseline export " +
        "[--profile <generation>] --run-dir \"<sealed-raw-run-directory>\" " +
        "[--comparison-run-dir \"<sealed-pg16-run-directory>\"] " +
        "--confirm-evidence-export";

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

        string? profile = null;
        string? connectionString = null;
        var confirmDisposable = false;
        string? containerName = null;
        string? runDirectory = null;
        string? comparisonRunDirectory = null;
        var confirmEvidenceExport = false;

        for (var index = optionsStartIndex; index < args.Length; index++)
        {
            switch (args[index])
            {
                case ProfileOption:
                    if (profile is not null)
                    {
                        error = $"Option '{ProfileOption}' may be supplied only once.";
                        return false;
                    }

                    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = $"Option '{ProfileOption}' requires a value.";
                        return false;
                    }

                    profile = args[++index].Trim();
                    break;

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
                case RunDirectoryAlias:
                    if (runDirectory is not null)
                    {
                        error =
                            $"Options '{RunDirectoryOption}'/'{RunDirectoryAlias}' may be " +
                            "supplied only once.";
                        return false;
                    }

                    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = $"Option '{RunDirectoryOption}' requires a value.";
                        return false;
                    }

                    runDirectory = args[++index].Trim();
                    break;

                case ComparisonRunDirectoryOption:
                    if (comparisonRunDirectory is not null)
                    {
                        error =
                            $"Option '{ComparisonRunDirectoryOption}' may be supplied only once.";
                        return false;
                    }

                    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        error = $"Option '{ComparisonRunDirectoryOption}' requires a value.";
                        return false;
                    }

                    comparisonRunDirectory = args[++index].Trim();
                    break;

                case ConfirmEvidenceExportOption:
                    if (confirmEvidenceExport)
                    {
                        error = $"Option '{ConfirmEvidenceExportOption}' may be supplied only once.";
                        return false;
                    }

                    confirmEvidenceExport = true;
                    break;

                default:
                    error = $"Unknown option '{args[index]}'.";
                    return false;
            }
        }

        if (command is QueryReviewCommand.BaselineVerify or QueryReviewCommand.BaselineExport)
        {
            if (connectionString is not null || confirmDisposable || containerName is not null)
            {
                error =
                    $"'{FormatCommand(command)}' is offline and rejects connection, disposable, and " +
                    "container options.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(runDirectory))
            {
                error = $"The offline baseline verifier requires '{RunDirectoryOption}'.";
                return false;
            }

            if (command == QueryReviewCommand.BaselineVerify && confirmEvidenceExport)
            {
                error =
                    $"Option '{ConfirmEvidenceExportOption}' is valid only for 'baseline export'.";
                return false;
            }

            if (command == QueryReviewCommand.BaselineExport && !confirmEvidenceExport)
            {
                error =
                    $"The permanent evidence acknowledgement '{ConfirmEvidenceExportOption}' " +
                    "is required.";
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = $"An explicit '{ConnectionStringOption}' value is required.";
            return false;
        }

        if (command is not QueryReviewCommand.BaselineVerify and
            not QueryReviewCommand.BaselineExport &&
            !confirmDisposable)
        {
            error = $"The safety acknowledgement '{ConfirmDisposableOption}' is required.";
            return false;
        }

        if ((command is QueryReviewCommand.ProfileCreate or QueryReviewCommand.BaselineRun) &&
            string.IsNullOrWhiteSpace(containerName))
        {
            error =
                $"'{FormatCommand(command)}' requires '{ContainerNameOption}' to identify " +
                "the exact running disposable PostgreSQL container.";
            return false;
        }

        if (command is not QueryReviewCommand.ProfileCreate and
            not QueryReviewCommand.BaselineRun &&
            containerName is not null)
        {
            error =
                $"Option '{ContainerNameOption}' is valid only for 'profile create' and " +
                "'baseline run'.";
            return false;
        }

        if (command is not QueryReviewCommand.BaselineVerify and
            not QueryReviewCommand.BaselineExport &&
            runDirectory is not null)
        {
            error =
                $"Option '{RunDirectoryOption}' is valid only for 'baseline verify' and " +
                "'baseline export'.";
            return false;
        }

        if (command is not QueryReviewCommand.Doctor and
            not QueryReviewCommand.BaselineVerify and
            not QueryReviewCommand.BaselineExport &&
            string.IsNullOrWhiteSpace(profile))
        {
            error =
                $"'{FormatCommand(command)}' requires explicit '{ProfileOption}' generation selection.";
            return false;
        }

        if (command != QueryReviewCommand.BaselineExport &&
            comparisonRunDirectory is not null)
        {
            error =
                $"Option '{ComparisonRunDirectoryOption}' is valid only for " +
                "'baseline export'.";
            return false;
        }

        if (comparisonRunDirectory is not null &&
            runDirectory is not null &&
            string.Equals(
                Path.GetFullPath(comparisonRunDirectory),
                Path.GetFullPath(runDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                $"Options '{RunDirectoryAlias}' and '{ComparisonRunDirectoryOption}' " +
                "must identify different sealed runs.";
            return false;
        }

        var outputDirectory = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "realestate-queryreview"));

        options = new QueryReviewOptions(
            command,
            profile,
            connectionString,
            confirmDisposable,
            outputDirectory,
            containerName,
            runDirectory is null ? null : Path.GetFullPath(runDirectory),
            comparisonRunDirectory is null
                ? null
                : Path.GetFullPath(comparisonRunDirectory),
            confirmEvidenceExport);

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

        if (args.Length > 1 &&
            string.Equals(args[0], "baseline", StringComparison.Ordinal) &&
            string.Equals(args[1], "export", StringComparison.Ordinal))
        {
            command = QueryReviewCommand.BaselineExport;
            optionsStartIndex = 2;
            return true;
        }

        error =
            "Supported commands are 'doctor', 'profile create', 'profile verify', and " +
            "'capture-sql', 'baseline run', 'baseline verify', and 'baseline export'.";
        return false;
    }

    internal static string FormatCommand(QueryReviewCommand command)
    {
        return command switch
        {
            QueryReviewCommand.Doctor => "doctor",
            QueryReviewCommand.ProfileCreate => "profile create",
            QueryReviewCommand.ProfileVerify => "profile verify",
            QueryReviewCommand.CaptureSql => "capture-sql",
            QueryReviewCommand.BaselineRun => "baseline run",
            QueryReviewCommand.BaselineVerify => "baseline verify",
            QueryReviewCommand.BaselineExport => "baseline export",
            _ => command.ToString()
        };
    }
}
