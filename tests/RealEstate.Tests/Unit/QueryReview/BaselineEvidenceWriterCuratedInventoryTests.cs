using System.Text.Json;
using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class BaselineEvidenceWriterCuratedInventoryTests
{
    private const string CommandKey = "curated-inventory-test-command";

    [Fact]
    public async Task PermanentExportInventory_AcceptsVerifiedCuratedSuccessorBundle()
    {
        string directory = await CreateVerifiedCuratedBundleAsync();

        try
        {
            Func<Task> act = () => ValidateAsync(directory);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PermanentExportInventory_RejectsMissingExperimentalManifest()
    {
        string directory = await CreateVerifiedCuratedBundleAsync();

        try
        {
            File.Delete(Path.Combine(
                directory,
                ExperimentalEvidenceBundle.ManifestFileName));

            Func<Task> act = () => ValidateAsync(directory);

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*Evidence file set mismatch*experimental-manifest.json*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PermanentExportInventory_RejectsUnrelatedExtraFile()
    {
        string directory = await CreateVerifiedCuratedBundleAsync();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "unrelated-extra.txt"),
                "unexpected\n");

            Func<Task> act = () => ValidateAsync(directory);

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*Evidence file set mismatch*unrelated-extra.txt*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PermanentExportInventory_RejectsTamperedExperimentalManifest()
    {
        string directory = await CreateVerifiedCuratedBundleAsync();

        try
        {
            string manifestPath = Path.Combine(
                directory,
                ExperimentalEvidenceBundle.ManifestFileName);
            ExperimentalEvidenceManifest manifest =
                JsonSerializer.Deserialize<ExperimentalEvidenceManifest>(
                    await File.ReadAllTextAsync(manifestPath),
                    JsonArtifactOutput.SerializerOptions)!;
            ArtifactHashEvidence firstArtifact = manifest.Artifacts[0];
            await JsonArtifactOutput.WriteAsync(
                manifestPath,
                manifest with
                {
                    Artifacts =
                    [
                        firstArtifact with { Sha256 = new string('0', 64) },
                        .. manifest.Artifacts.Skip(1)
                    ]
                });

            Func<Task> act = () => ValidateAsync(directory);

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*Experimental artifact hash mismatch*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task ValidateAsync(string directory) =>
        BaselineEvidenceWriter.ValidateCuratedExportInputAsync(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId),
            directory,
            [CommandKey]);

    private static async Task<string> CreateVerifiedCuratedBundleAsync()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"queryreview-curated-inventory-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "sql"));
        Directory.CreateDirectory(Path.Combine(directory, "baseline-plans"));

        await File.WriteAllTextAsync(
            Path.Combine(directory, "environment.json"),
            "environment-test\n");
        await File.WriteAllTextAsync(
            Path.Combine(directory, "baseline-measurements.json"),
            "measurements-test\n");
        await File.WriteAllTextAsync(
            Path.Combine(directory, "baseline-summary.md"),
            "# Test summary\n");
        await File.WriteAllTextAsync(
            Path.Combine(directory, "sql", $"{CommandKey}.sql"),
            "SELECT 1;\n");
        await File.WriteAllTextAsync(
            Path.Combine(directory, "baseline-plans", $"{CommandKey}.json"),
            "plan-test\n");

        await ExperimentalEvidenceBundle.WriteManifestAsync(
            directory,
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId),
            "curated-inventory-test-run");

        return directory;
    }
}
