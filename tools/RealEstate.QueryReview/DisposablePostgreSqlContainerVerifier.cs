using System.Globalization;
using System.Text.Json;
using Npgsql;

namespace RealEstate.QueryReview;

internal static class DisposablePostgreSqlContainerVerifier
{
    private const string RequiredContainerImage = "postgres:16-alpine";
    private const string PostgreSqlContainerPort = "5432/tcp";
    private const string PostgreSqlDataPath = "/var/lib/postgresql/data";
    private static readonly TimeSpan DockerInspectionTimeout = TimeSpan.FromSeconds(15);

    public static Task VerifyAsync(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        return VerifyAsync(
            connectionStringBuilder,
            containerName,
            EnvironmentSnapshotCollector.RunProcessAsync,
            cancellationToken);
    }

    public static Task VerifyForLaneAsync(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string containerName,
        QueryReviewLaneDefinition lane,
        CancellationToken cancellationToken = default)
    {
        EnsureCatalogLane(lane);

        if (!lane.RequireImageDeclaredAnonymousVolume)
        {
            throw new BaselinePlanValidationException(
                $"Lane '{lane.Id}' does not declare the required disposable storage policy.");
        }

        return VerifyCoreAsync(
            connectionStringBuilder,
            containerName,
            lane.RequiredContainerImage,
            lane.PostgreSqlDataPath,
            EnvironmentSnapshotCollector.RunProcessAsync,
            cancellationToken);
    }

    internal static Task VerifyForLaneAsync(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string containerName,
        QueryReviewLaneDefinition lane,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> processRunner,
        CancellationToken cancellationToken = default)
    {
        EnsureCatalogLane(lane);

        if (!lane.RequireImageDeclaredAnonymousVolume)
        {
            throw new BaselinePlanValidationException(
                $"Lane '{lane.Id}' does not declare the required disposable storage policy.");
        }

        return VerifyCoreAsync(
            connectionStringBuilder,
            containerName,
            lane.RequiredContainerImage,
            lane.PostgreSqlDataPath,
            processRunner,
            cancellationToken);
    }

    private static void EnsureCatalogLane(QueryReviewLaneDefinition lane)
    {
        if (!QueryReviewGenerations.All
                .SelectMany(generation => generation.Lanes)
                .Any(candidate => ReferenceEquals(candidate, lane)))
        {
            throw new BaselinePlanValidationException(
                "Disposable PostgreSQL verification accepts only an exact catalog lane.");
        }
    }

    internal static async Task VerifyAsync(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string containerName,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> processRunner,
        CancellationToken cancellationToken = default)
    {
        await VerifyCoreAsync(
            connectionStringBuilder,
            containerName,
            RequiredContainerImage,
            PostgreSqlDataPath,
            processRunner,
            cancellationToken);
    }

    private static async Task VerifyCoreAsync(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string containerName,
        string requiredContainerImage,
        string postgreSqlDataPath,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> processRunner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectionStringBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentNullException.ThrowIfNull(processRunner);

        var hostKind = GetSupportedLocalHostKind(connectionStringBuilder.Host ?? string.Empty);

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(DockerInspectionTimeout);

        string dockerEndpointJson;
        string inspectionJson;

        try
        {
            dockerEndpointJson = await processRunner(
                "docker",
                ["context", "inspect", "--format", "{{json .Endpoints.docker.Host}}"],
                timeoutCancellation.Token);
            VerifyLocalDockerEngine(dockerEndpointJson);

            inspectionJson = await processRunner(
                "docker",
                ["inspect", "--type", "container", containerName],
                timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection for container '{containerName}' timed out.");
        }

        VerifyInspection(
            inspectionJson,
            containerName,
            hostKind,
            connectionStringBuilder.Port,
            requiredContainerImage,
            postgreSqlDataPath);
    }

    internal static void VerifyLocalDockerEngine(string dockerEndpointJson)
    {
        try
        {
            var endpoint = JsonSerializer.Deserialize<string>(dockerEndpointJson)?.Trim();

            if (string.IsNullOrWhiteSpace(endpoint) ||
                (!endpoint.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase) &&
                 !endpoint.StartsWith("unix://", StringComparison.OrdinalIgnoreCase)))
            {
                throw new BaselinePlanValidationException(
                    "Profile creation requires a local Docker engine exposed through a named " +
                    "pipe or Unix socket; remote Docker contexts are rejected.");
            }
        }
        catch (JsonException exception)
        {
            throw new BaselinePlanValidationException(
                $"Docker context inspection returned malformed JSON: {exception.Message}");
        }
    }

    internal static void VerifyInspection(
        string inspectionJson,
        string containerName,
        SupportedLocalHostKind hostKind,
        int suppliedPort,
        string requiredContainerImage = RequiredContainerImage,
        string postgreSqlDataPath = PostgreSqlDataPath)
    {
        try
        {
            using var document = JsonDocument.Parse(inspectionJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1)
            {
                throw new BaselinePlanValidationException(
                    "Docker inspection must identify exactly one container.");
            }

            var container = root[0];
            var inspectedName = ReadRequiredString(container, "Name").TrimStart('/');

            if (!string.Equals(inspectedName, containerName, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Docker inspected container '{inspectedName}', not the exact requested " +
                    $"container '{containerName}'.");
            }

            var state = ReadRequiredObject(container, "State");
            var isRunning = ReadRequiredBoolean(state, "Running");
            var status = ReadRequiredString(state, "Status");

            if (!isRunning || !string.Equals(status, "running", StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Docker container '{containerName}' is not running.");
            }

            var configuration = ReadRequiredObject(container, "Config");
            var image = ReadRequiredString(configuration, "Image");

            if (!string.Equals(image, requiredContainerImage, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Docker container '{containerName}' uses image '{image}'; expected " +
                    $"'{requiredContainerImage}'.");
            }

            var hostConfiguration = ReadRequiredObject(container, "HostConfig");

            if (!ReadRequiredBoolean(hostConfiguration, "AutoRemove"))
            {
                throw new BaselinePlanValidationException(
                    $"Docker container '{containerName}' is not disposable because AutoRemove " +
                    "is disabled. Start it with '--rm'.");
            }

            VerifyDisposableStorage(
                container,
                configuration,
                hostConfiguration,
                containerName,
                postgreSqlDataPath);

            var networkSettings = ReadRequiredObject(container, "NetworkSettings");
            var ports = ReadRequiredObject(networkSettings, "Ports");

            if (!ports.TryGetProperty(PostgreSqlContainerPort, out var bindings) ||
                bindings.ValueKind != JsonValueKind.Array ||
                bindings.GetArrayLength() == 0)
            {
                throw new BaselinePlanValidationException(
                    $"Docker container '{containerName}' does not publish PostgreSQL port " +
                    $"{PostgreSqlContainerPort}.");
            }

            var publishedPorts = new HashSet<int>();
            var hostBindingMatches = false;

            foreach (var binding in bindings.EnumerateArray())
            {
                var hostIp = ReadRequiredString(binding, "HostIp");
                var hostPortValue = ReadRequiredString(binding, "HostPort");

                if (!int.TryParse(
                        hostPortValue,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var hostPort) ||
                    hostPort is < 1 or > 65_535)
                {
                    throw new BaselinePlanValidationException(
                        $"Docker returned invalid PostgreSQL host port '{hostPortValue}'.");
                }

                publishedPorts.Add(hostPort);
                hostBindingMatches |= BindingSupportsHost(hostIp, hostKind);
            }

            if (publishedPorts.Count != 1)
            {
                throw new BaselinePlanValidationException(
                    $"Docker container '{containerName}' has ambiguous PostgreSQL published " +
                    "host ports.");
            }

            var publishedPort = publishedPorts.Single();

            if (publishedPort != suppliedPort)
            {
                throw new BaselinePlanValidationException(
                    $"The supplied PostgreSQL port {suppliedPort} does not match container " +
                    $"'{containerName}' published port {publishedPort}.");
            }

            if (!hostBindingMatches)
            {
                throw new BaselinePlanValidationException(
                    $"The supplied local PostgreSQL host is not exposed by container " +
                    $"'{containerName}' PostgreSQL port binding.");
            }
        }
        catch (JsonException exception)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection returned malformed JSON: {exception.Message}");
        }
    }

    private static void VerifyDisposableStorage(
        JsonElement container,
        JsonElement configuration,
        JsonElement hostConfiguration,
        string containerName,
        string postgreSqlDataPath)
    {
        VerifyNullOrEmptyArray(hostConfiguration, "Binds", containerName);
        VerifyNullOrEmptyArray(
            hostConfiguration,
            "Mounts",
            containerName,
            allowMissing: true);
        VerifyNullOrEmptyArray(hostConfiguration, "VolumesFrom", containerName);
        VerifyNullOrEmptyObject(
            hostConfiguration,
            "Tmpfs",
            containerName,
            allowMissing: true);

        if (!hostConfiguration.TryGetProperty("VolumeDriver", out var volumeDriver) ||
            volumeDriver.ValueKind != JsonValueKind.String ||
            !string.IsNullOrEmpty(volumeDriver.GetString()))
        {
            throw new BaselinePlanValidationException(
                $"Docker container '{containerName}' uses a configured volume driver; " +
                "QueryReview requires Docker's default anonymous volume lifecycle.");
        }

        var declaredVolumes = ReadRequiredObject(configuration, "Volumes");

        if (declaredVolumes.EnumerateObject().Count() != 1 ||
            !declaredVolumes.TryGetProperty(postgreSqlDataPath, out _))
        {
            throw new BaselinePlanValidationException(
                $"Docker container '{containerName}' does not expose exactly the expected " +
                $"image-declared PostgreSQL volume '{postgreSqlDataPath}'.");
        }

        var runtimeMounts = ReadRequiredArray(container, "Mounts");

        if (runtimeMounts.GetArrayLength() != 1)
        {
            throw new BaselinePlanValidationException(
                $"Docker container '{containerName}' must have exactly one runtime mount: " +
                $"the image-created anonymous PostgreSQL volume at '{postgreSqlDataPath}'.");
        }

        var runtimeMount = runtimeMounts[0];

        if (runtimeMount.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadRequiredString(runtimeMount, "Type"),
                "volume",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadRequiredString(runtimeMount, "Destination"),
                postgreSqlDataPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadRequiredString(runtimeMount, "Driver"),
                "local",
                StringComparison.Ordinal) ||
            !ReadRequiredBoolean(runtimeMount, "RW"))
        {
            throw new BaselinePlanValidationException(
                $"Docker container '{containerName}' runtime storage is not the expected " +
                $"writable local anonymous PostgreSQL volume at '{postgreSqlDataPath}'.");
        }

        _ = ReadRequiredString(runtimeMount, "Name");
        _ = ReadRequiredString(runtimeMount, "Source");
    }

    private static void VerifyNullOrEmptyArray(
        JsonElement owner,
        string propertyName,
        string containerName,
        bool allowMissing = false)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            if (allowMissing)
            {
                return;
            }

            throw new BaselinePlanValidationException(
                $"Docker inspection omitted valid '{propertyName}' storage metadata.");
        }

        if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Array)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection omitted valid '{propertyName}' storage metadata.");
        }

        if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() != 0)
        {
            throw new BaselinePlanValidationException(
                $"Docker container '{containerName}' has user-configured '{propertyName}' " +
                "storage; QueryReview permits only the image-created anonymous data volume.");
        }
    }

    private static void VerifyNullOrEmptyObject(
        JsonElement owner,
        string propertyName,
        string containerName,
        bool allowMissing = false)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            if (allowMissing)
            {
                return;
            }

            throw new BaselinePlanValidationException(
                $"Docker inspection omitted valid '{propertyName}' storage metadata.");
        }

        if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Object)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection omitted valid '{propertyName}' storage metadata.");
        }

        if (value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any())
        {
            throw new BaselinePlanValidationException(
                $"Docker container '{containerName}' has user-configured '{propertyName}' " +
                "storage; QueryReview permits only the image-created anonymous data volume.");
        }
    }

    private static SupportedLocalHostKind GetSupportedLocalHostKind(string host)
    {
        return host.Trim() switch
        {
            var value when string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) =>
                SupportedLocalHostKind.Localhost,
            "127.0.0.1" => SupportedLocalHostKind.Ipv4Loopback,
            "::1" or "[::1]" => SupportedLocalHostKind.Ipv6Loopback,
            _ => throw new BaselinePlanValidationException(
                "Profile creation accepts only local Docker endpoints using 'localhost', " +
                "'127.0.0.1', or '::1'.")
        };
    }

    private static bool BindingSupportsHost(
        string hostIp,
        SupportedLocalHostKind hostKind)
    {
        return hostKind switch
        {
            SupportedLocalHostKind.Localhost =>
                hostIp is "0.0.0.0" or "127.0.0.1" or "::" or "::1",
            SupportedLocalHostKind.Ipv4Loopback => hostIp is "0.0.0.0" or "127.0.0.1",
            SupportedLocalHostKind.Ipv6Loopback => hostIp is "::" or "::1",
            _ => false
        };
    }

    private static JsonElement ReadRequiredObject(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection omitted object '{propertyName}'.");
        }

        return value;
    }

    private static JsonElement ReadRequiredArray(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection omitted array '{propertyName}'.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection omitted string '{propertyName}'.");
        }

        return value.GetString()!;
    }

    private static bool ReadRequiredBoolean(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspection omitted boolean '{propertyName}'.");
        }

        return value.GetBoolean();
    }
}

internal enum SupportedLocalHostKind
{
    Localhost,
    Ipv4Loopback,
    Ipv6Loopback
}
