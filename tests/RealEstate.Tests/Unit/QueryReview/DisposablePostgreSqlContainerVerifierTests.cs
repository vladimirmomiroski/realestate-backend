using System.Text.Json;
using FluentAssertions;
using Npgsql;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class DisposablePostgreSqlContainerVerifierTests
{
    private const string ContainerName = "realestate-queryreview-postgres16-test";

    [Fact]
    public void ProfileCreate_RequiresExplicitContainerName()
    {
        var parsed = QueryReviewOptions.TryParse(
            [
                "profile",
                "create",
                "--profile",
                QueryReviewGenerations.FourRootDiscoveryId,
                "--connection-string",
                ValidConnectionString(),
                "--confirm-disposable"
            ],
            out _,
            out var error);

        parsed.Should().BeFalse();
        error.Should().Contain("--container-name");
    }

    [Fact]
    public void ProfileVerify_RequiresExplicitContainerName()
    {
        var parsed = QueryReviewOptions.TryParse(
            [
                "profile",
                "verify",
                "--profile",
                QueryReviewGenerations.FourRootDiscoveryId,
                "--connection-string",
                ValidConnectionString(),
                "--confirm-disposable"
            ],
            out _,
            out var error);

        parsed.Should().BeFalse();
        error.Should().Contain("--container-name");
    }

    [Theory]
    [InlineData("localhost", "0.0.0.0")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("::1", "::")]
    public async Task MatchingRunningDisposableContainerWithAnonymousDataVolume_IsAccepted(
        string host,
        string publishedHostIp)
    {
        var builder = ConnectionString(host, 55_442);
        var processCalled = false;

        await DisposablePostgreSqlContainerVerifier.VerifyAsync(
            builder,
            ContainerName,
            (fileName, arguments, _) =>
            {
                processCalled = true;
                fileName.Should().Be("docker");

                if (arguments[0] == "context")
                {
                    arguments.Should().Equal(
                        "context",
                        "inspect",
                        "--format",
                        "{{json .Endpoints.docker.Host}}");
                    return Task.FromResult(JsonSerializer.Serialize(
                        "npipe:////./pipe/dockerDesktopLinuxEngine"));
                }

                arguments.Should().Equal("inspect", "--type", "container", ContainerName);
                return Task.FromResult(Inspection(publishedHostIp: publishedHostIp));
            });

        processCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OmittedDockerNullStorageFieldsWithAnonymousDataVolume_IsAccepted()
    {
        await VerifyWithInspectionAsync(
            Inspection(includeDockerOmittedNullStorageMetadata: false));
    }

    [Fact]
    public async Task PostgreSql184Lane_AcceptsVersionAwareImageDeclaredVolume()
    {
        QueryReviewLaneDefinition lane = QueryReviewGenerations.FourRootDiscovery.RequireLane(
            QueryReviewGenerations.PostgreSql184LaneId);

        await VerifyLaneWithInspectionAsync(
            lane,
            Inspection(
                image: "postgres:18.4",
                declaredVolumePath: "/var/lib/postgresql",
                runtimeMounts:
                [
                    RuntimeMount(destination: "/var/lib/postgresql")
                ]));
    }

    [Fact]
    public async Task PostgreSql16Lane_AcceptsExistingDataVolumeLayout()
    {
        QueryReviewLaneDefinition lane = QueryReviewGenerations.FourRootDiscovery.RequireLane(
            QueryReviewGenerations.PostgreSql16LaneId);

        await VerifyLaneWithInspectionAsync(lane, Inspection());
    }

    [Fact]
    public async Task PostgreSql184Lane_RejectsPre18DataVolumeLayout()
    {
        QueryReviewLaneDefinition lane = QueryReviewGenerations.FourRootDiscovery.RequireLane(
            QueryReviewGenerations.PostgreSql184LaneId);

        Func<Task> act = () => VerifyLaneWithInspectionAsync(
            lane,
            Inspection(image: "postgres:18.4"));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*image-declared PostgreSQL volume '/var/lib/postgresql'*");
    }

    [Fact]
    public async Task StoppedContainer_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(running: false, status: "exited"));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*not running*");
    }

    [Fact]
    public async Task NonDisposableContainer_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(autoRemove: false));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*AutoRemove*disabled*");
    }

    [Fact]
    public async Task BindMountToPostgreSqlData_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(
                hostBinds: ["C:\\queryreview-data:/var/lib/postgresql/data"],
                runtimeMounts:
                [
                    RuntimeMount(
                        type: "bind",
                        name: "queryreview-data",
                        source: "C:\\queryreview-data")
                ]));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*user-configured 'Binds' storage*");
    }

    [Fact]
    public async Task NamedVolumeToPostgreSqlData_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(
                hostBinds: ["queryreview-postgres-data:/var/lib/postgresql/data"],
                runtimeMounts:
                [
                    RuntimeMount(name: "queryreview-postgres-data")
                ]));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*user-configured 'Binds' storage*");
    }

    [Fact]
    public async Task StructuredMountVolumeToPostgreSqlData_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(
                hostMounts:
                [
                    new
                    {
                        Type = "volume",
                        Source = "queryreview-postgres-data",
                        Target = "/var/lib/postgresql/data"
                    }
                ],
                runtimeMounts:
                [
                    RuntimeMount(name: "queryreview-postgres-data")
                ]));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*user-configured 'Mounts' storage*");
    }

    [Fact]
    public async Task AdditionalUnsupportedRuntimeMount_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(
                runtimeMounts:
                [
                    RuntimeMount(),
                    RuntimeMount(
                        type: "bind",
                        name: "queryreview-extra",
                        source: "C:\\queryreview-extra",
                        destination: "/queryreview-extra")
                ]));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*must have exactly one runtime mount*");
    }

    [Fact]
    public async Task MissingStorageInspectionMetadata_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(includeStorageMetadata: false));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*omitted valid 'Binds' storage metadata*");
    }

    [Fact]
    public async Task WrongContainerImage_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(image: "custom-postgres:16"));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*expected*postgres:16-alpine*");
    }

    [Fact]
    public async Task PublishedPortMismatch_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(publishedHostPort: "55443"));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*supplied PostgreSQL port 55442*published port 55443*");
    }

    [Theory]
    [InlineData("remote-server")]
    [InlineData("203.0.113.10")]
    public async Task ExternalHost_IsRejectedBeforeDockerInspection(string host)
    {
        var processCalled = false;
        Func<Task> act = () => DisposablePostgreSqlContainerVerifier.VerifyAsync(
            ConnectionString(host, 55_442),
            ContainerName,
            (_, _, _) =>
            {
                processCalled = true;
                return Task.FromResult(Inspection());
            });

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*only local Docker endpoints*");
        processCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DockerInspectionFailure_FailsClosed()
    {
        Func<Task> act = () => DisposablePostgreSqlContainerVerifier.VerifyAsync(
            ConnectionString("localhost", 55_442),
            ContainerName,
            (_, arguments, _) => arguments[0] == "context"
                ? Task.FromResult(JsonSerializer.Serialize("unix:///var/run/docker.sock"))
                : throw new BaselinePlanValidationException(
                    "Environment command 'docker' exited 1: container not found"));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*container not found*");
    }

    [Fact]
    public async Task RemoteDockerContext_IsRejectedBeforeContainerInspection()
    {
        var containerInspectionCalled = false;
        Func<Task> act = () => DisposablePostgreSqlContainerVerifier.VerifyAsync(
            ConnectionString("localhost", 55_442),
            ContainerName,
            (_, arguments, _) =>
            {
                if (arguments[0] == "context")
                {
                    return Task.FromResult(JsonSerializer.Serialize(
                        "tcp://remote-docker.example:2376"));
                }

                containerInspectionCalled = true;
                return Task.FromResult(Inspection());
            });

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*remote Docker contexts are rejected*");
        containerInspectionCalled.Should().BeFalse();
    }

    [Fact]
    public async Task MissingPostgreSqlPortPublication_IsRejected()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(publishPostgreSqlPort: false));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*does not publish PostgreSQL port 5432/tcp*");
    }

    [Fact]
    public async Task DockerNameResolutionMustMatchExactRequestedContainer()
    {
        Func<Task> act = () => VerifyWithInspectionAsync(
            Inspection(inspectedName: $"{ContainerName}-other"));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*not the exact requested container*");
    }

    [Fact]
    public async Task UnsupportedBindingForRequestedLoopbackFamily_IsRejected()
    {
        Func<Task> act = () => DisposablePostgreSqlContainerVerifier.VerifyAsync(
            ConnectionString("127.0.0.1", 55_442),
            ContainerName,
            (_, arguments, _) => Task.FromResult(
                DockerOutput(arguments, Inspection(publishedHostIp: "::"))));

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*not exposed by container*");
    }

    private static Task VerifyWithInspectionAsync(string inspection)
    {
        return DisposablePostgreSqlContainerVerifier.VerifyAsync(
            ConnectionString("localhost", 55_442),
            ContainerName,
            (_, arguments, _) => Task.FromResult(DockerOutput(arguments, inspection)));
    }

    private static Task VerifyLaneWithInspectionAsync(
        QueryReviewLaneDefinition lane,
        string inspection)
    {
        return DisposablePostgreSqlContainerVerifier.VerifyForLaneAsync(
            ConnectionString("localhost", 55_442),
            ContainerName,
            lane,
            (_, arguments, _) => Task.FromResult(DockerOutput(arguments, inspection)));
    }

    private static NpgsqlConnectionStringBuilder ConnectionString(string host, int port)
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = "realestate_queryreview_safety_test",
            Username = "postgres",
            Password = "not-logged"
        };
    }

    private static string ValidConnectionString()
    {
        return ConnectionString("localhost", 55_442).ConnectionString;
    }

    private static string Inspection(
        string inspectedName = ContainerName,
        bool running = true,
        string status = "running",
        bool autoRemove = true,
        string image = "postgres:16-alpine",
        bool publishPostgreSqlPort = true,
        string publishedHostIp = "0.0.0.0",
        string publishedHostPort = "55442",
        string[]? hostBinds = null,
        object[]? hostMounts = null,
        object[]? runtimeMounts = null,
        bool includeStorageMetadata = true,
        bool includeDockerOmittedNullStorageMetadata = true,
        string declaredVolumePath = "/var/lib/postgresql/data")
    {
        var ports = new Dictionary<string, object?>();

        if (publishPostgreSqlPort)
        {
            ports["5432/tcp"] = new[]
            {
                new { HostIp = publishedHostIp, HostPort = publishedHostPort }
            };
        }

        var container = new Dictionary<string, object?>
        {
            ["Id"] = "0123456789abcdef",
            ["Name"] = $"/{inspectedName}",
            ["State"] = new { Running = running, Status = status },
            ["Config"] = new
            {
                Image = image,
                Volumes = new Dictionary<string, object?>
                {
                    [declaredVolumePath] = new { }
                }
            },
            ["NetworkSettings"] = new { Ports = ports }
        };

        if (includeStorageMetadata)
        {
            var hostConfiguration = new Dictionary<string, object?>
            {
                ["AutoRemove"] = autoRemove,
                ["Binds"] = hostBinds,
                ["VolumesFrom"] = (string[]?)null,
                ["VolumeDriver"] = string.Empty
            };

            if (includeDockerOmittedNullStorageMetadata)
            {
                hostConfiguration["Mounts"] = hostMounts;
                hostConfiguration["Tmpfs"] =
                    (Dictionary<string, string>?)null;
            }

            container["HostConfig"] = hostConfiguration;
            container["Mounts"] = runtimeMounts ?? [RuntimeMount()];
        }
        else
        {
            container["HostConfig"] = new { AutoRemove = autoRemove };
        }

        return JsonSerializer.Serialize(new[] { container });
    }

    private static object RuntimeMount(
        string type = "volume",
        string name = "anonymous-queryreview-volume-id",
        string source = "/var/lib/docker/volumes/anonymous-queryreview-volume-id/_data",
        string destination = "/var/lib/postgresql/data")
    {
        return new
        {
            Type = type,
            Name = name,
            Source = source,
            Destination = destination,
            Driver = "local",
            RW = true
        };
    }

    private static string DockerOutput(
        IReadOnlyList<string> arguments,
        string inspection)
    {
        return arguments[0] == "context"
            ? JsonSerializer.Serialize("unix:///var/run/docker.sock")
            : inspection;
    }
}
