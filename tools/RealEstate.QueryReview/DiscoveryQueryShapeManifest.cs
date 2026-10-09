using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealEstate.QueryReview;

internal sealed record QueryShapeContractDefinition(
    string GenerationId,
    string ProfileIdentity,
    string LogicalRunId,
    int ShapeCount,
    int CommandCount,
    int TypedParameterCount,
    int PlanCount,
    int ProfileInvariantCount,
    IReadOnlyList<string> CommandKeys,
    string? ExpectedManifestSha256);

internal static class DiscoveryQueryShapeManifest
{
    private const int RunsPerCommand = 6;

    private static readonly JsonSerializerOptions CanonicalInputSerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    internal const string ExpectedSuccessorManifestSha256 =
        "fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979";
    internal const string ExpectedSuccessorResultIdentitySha256 =
        "975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19";

    internal static readonly QueryShapeResultIdentity AcceptedAgencyLandDeepPageResult = new(
        QueryShapeDefinitions.AgencyLandAgriculturalDeepPage,
        TotalCount: 71,
        ItemCount: 11,
        OrderedIds:
        [
            Guid.Parse("40000000-0000-0000-0000-00000000d301"),
            Guid.Parse("40000000-0000-0000-0000-000000007d11"),
            Guid.Parse("40000000-0000-0000-0000-000000001b69"),
            Guid.Parse("40000000-0000-0000-0000-00000000bf77"),
            Guid.Parse("40000000-0000-0000-0000-000000005dcf"),
            Guid.Parse("40000000-0000-0000-0000-000000004a45"),
            Guid.Parse("40000000-0000-0000-0000-000000000fa9"),
            Guid.Parse("40000000-0000-0000-0000-0000000109a7"),
            Guid.Parse("40000000-0000-0000-0000-00000000520f"),
            Guid.Parse("40000000-0000-0000-0000-000000009475"),
            Guid.Parse("40000000-0000-0000-0000-00000000d6db")
        ],
        OrderedIdsSha256: "1b0a6a8f792c7adccbb27ecddcfe63f540c26a5b11033cb691b168c946bbf29f");

    private static readonly IReadOnlyDictionary<string, string>
        AcceptedHistoricalMappingSqlSha256 = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["N1-01-filtered-count"] = "71d892484ec51372fa196a1c61ba1f6d4922e85cd1b6de54c86bdba554db148f",
            ["N1-02-page-root"] = "b6dae805d9a2592ec92cdf7718c3a3b16a5f71c4b87b57e4620a2f8f76b75198",
            ["N1-03-translation-split"] = "b37c0c307121df1df5d6119dcb576d278beff0451d32811844ed2a90e75c5403",
            ["N1-04-image-split"] = "b48092b5fd5ce53853d5be89a99c577b6f3dce478f221f797001532a3673ad1a",
            ["P1-01-filtered-count"] = "7e7cbca5ef4d4bc0c972a3614cdc0298f25d98c6df920a98b6d6d819823d9154",
            ["P1-02-page-root"] = "a3a4cdd0c595c014025aeadf160a8038dc4fb1704a006bd9e800549515a14b2f",
            ["P1-03-translation-split"] = "e4ccdc85713fd6d6fcee8d49b45bf969ebcc1c85bf56fc8225fea6ff68f1a653",
            ["P1-04-image-split"] = "8b83fea33c68b4053ea5fd026ec2577976114dbce781f2aed6c27d601b8fd922",
            ["P2-01-filtered-count"] = "b845629ded9b79684afceb9f1da29e85881a0e25fbc778adbe0a76b08e46fa97",
            ["P2-02-page-root"] = "ac220a9528f7356020291c8d1613ed8182cb999e03a6fcbbda83ba0195374e82",
            ["P2-03-translation-split"] = "0020227ffae96e4c3acf6abe79403d4cc95c5b42121f7ce297ce9e97c9b12165",
            ["P2-04-image-split"] = "a1ddac6b9ed5a6d51e923c16e079b21a108a274af2bca337fb797ed0f2c8c89e",
            ["A1-01-agency-existence"] = "6957da296e3df9a17006b00cdf7862ca144caecc6d5c25bf6608e8dd0ab9caa3",
            ["A1-02-filtered-count"] = "a506edf16752b908341e0f558d979124d9bd7718758151b7e8bae5bd9dfa363c",
            ["A1-03-page-root"] = "363713fac9da459fb8e53dd4fea0ba1a0de1df26886f151430edd1e97516f836",
            ["A1-04-translation-split"] = "42d6909ae55df01699676218496223e7ff1afe65c1e89a9066a44638200bca17",
            ["A1-05-image-split"] = "47118f996169c5eaec50e9daf97184b5a46ed53bd8605adbd1f30b2fd789409b",
            ["R1-01-filtered-count"] = "e670819b7f690e0cc3c7bee6160226152431feb1929cd9dd797114e69b6760ed",
            ["R1-02-page-root"] = "dc5dd17d148404863f68851cbfd31cd5b4fde40061974344dc3078ce851ddd94",
            ["R1-03-translation-split"] = "9fb4ad29408df9ae8a50997191b4796ad1506a7e809b8a094e8af7d533ca3568",
            ["R1-04-image-split"] = "2fc8bea5437b48e10534bb17a20af4515a878545cfa249f447ce457ed7978072",
            ["L1-01-filtered-count"] = "b796b29ddd815aa3e46243794b90ed937344f81ef811bcdb0c00b059b3e03bff",
            ["L1-02-page-root"] = "c1044a40ea63f3f22d22095af5db93c3f6a5cf86956f574f840702b8e53d6a22",
            ["L1-03-translation-split"] = "c69350705a93687913a84ec6872d74a4416465aef13728833b8474bd40e1cd62",
            ["L1-04-image-split"] = "c1b1626cb8559db294ec739e29eb53339f605b289c5f22907a9d773d2c519814",
            ["Q1-01-filtered-count"] = "024b959782407204569ce8f26427d2ee4814c31df854937e8cd0385c1d2bbce7",
            ["Q1-02-page-root"] = "e18b77b9b47ef4a46f826c2f21d77e8a67a3ccf00b61515daf597ecd99ae75ee",
            ["Q1-03-translation-split"] = "a95a01bc39823b3067b0cd51a70ee380ece5dcdcc80eaa9b69708330126845a0",
            ["Q1-04-image-split"] = "6de558d72f3dcac53389ca493ccb931b8a728c34aed9ee518cdfc8aaf2a6ce2e",
            ["C1-01-comparable-source"] = "c676a6ba3f9752ef1a91862ad0df2051416bd51a4bed37a15b170c4c860f72a4",
            ["C1-02-comparable-ranked-root"] = "f2c288ed5b8b592703a929f464d948119b53517a9767fbef972c6cc0422eb215",
            ["C1-03-comparable-translation-split"] = "d105d7ed7dc5296a458706fc0e183cff3967c1979747639f6dbed81fff6d7396",
            ["C1-04-comparable-image-split"] = "8a2bf5088ecfce33e4b295ab559a5bef2c678f320c367e50d49ffac58bd30a73"
        };

    internal static QueryShapeContractDefinition GetDefinition(
        QueryReviewGenerationDefinition generation)
    {
        IReadOnlyList<string> commandKeys = BuildExpectedCommandKeys(generation);
        bool successor = generation == QueryReviewGenerations.FourRootDiscovery;
        int shapeCount = successor ? 21 : 8;
        int parameterCount = successor ? 190 : 80;
        int invariantCount = successor ? FourRootProfileInvariants.InvariantCount : 61;

        return new QueryShapeContractDefinition(
            generation.Id,
            successor
                ? generation.ProfileIdentity
                : DeterministicProfileSeeder.ProfileVersion,
            successor
                ? QueryShapeDefinitions.FourRootLogicalRunId
                : QueryShapeDefinitions.LogicalRunId,
            shapeCount,
            commandKeys.Count,
            parameterCount,
            commandKeys.Count * RunsPerCommand,
            invariantCount,
            commandKeys,
            successor ? ExpectedSuccessorManifestSha256 : null);
    }

    internal static SqlCaptureRun BindAndValidate(
        QueryReviewGenerationDefinition generation,
        SqlCaptureRun captureRun)
    {
        QueryShapeContractDefinition definition = GetDefinition(generation);
        ValidateStructure(definition, captureRun);

        if (generation != QueryReviewGenerations.FourRootDiscovery)
        {
            return captureRun;
        }

        ValidateMappedHistoricalCommands(captureRun.Commands);
        QueryShapeManifestSnapshot snapshot = CreateSuccessorSnapshot(captureRun, definition);
        string actualSha256 = ComputeManifestSha256(snapshot);

        if (!string.Equals(
                definition.ExpectedManifestSha256,
                actualSha256,
                StringComparison.Ordinal))
        {
            throw new SqlCaptureValidationException(
                "Successor query-shape manifest identity drifted. " +
                $"Expected {definition.ExpectedManifestSha256}, actual {actualSha256}.");
        }

        ValidateEquivalenceAndEmptyContracts(snapshot);
        return captureRun with { QueryShapeManifest = snapshot };
    }

    internal static string ValidateRecordedManifest(
        QueryReviewGenerationDefinition generation,
        SqlCaptureRun captureRun)
    {
        SqlCaptureRun validated = BindAndValidate(
            generation,
            captureRun with { QueryShapeManifest = null });

        if (generation != QueryReviewGenerations.FourRootDiscovery)
        {
            return string.Empty;
        }

        QueryShapeManifestSnapshot expected = validated.QueryShapeManifest
            ?? throw new BaselinePlanValidationException(
                "Successor capture validation did not produce a query-shape manifest.");
        QueryShapeManifestSnapshot recorded = captureRun.QueryShapeManifest
            ?? throw new BaselinePlanValidationException(
                "Successor capture is missing its query-shape manifest.");
        ValidateRecordedInputIdentities(recorded.Inputs);
        string expectedJson = JsonSerializer.Serialize(
            expected,
            JsonArtifactOutput.SerializerOptions);
        string recordedJson = JsonSerializer.Serialize(
            recorded,
            JsonArtifactOutput.SerializerOptions);

        if (!string.Equals(expectedJson, recordedJson, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Recorded successor query-shape manifest does not match the captured commands, " +
                "inputs, results, roles, or totals.");
        }

        return ComputeManifestSha256(recorded);
    }

    internal static void ValidateStructure(
        QueryShapeContractDefinition definition,
        SqlCaptureRun captureRun)
    {
        string[] shapeIds = captureRun.ShapeResults
            .Select(result => result.ShapeId)
            .ToArray();
        string[] expectedShapeIds = definition.GenerationId ==
                                    QueryReviewGenerations.FourRootDiscoveryId
            ? [.. QueryShapeDefinitions.LegacyShapeIds,
                .. QueryShapeDefinitions.SuccessorDiscoveryShapeIds]
            : QueryShapeDefinitions.LegacyShapeIds.ToArray();
        string[] commandKeys = captureRun.Commands
            .Select(CreateCommandKey)
            .ToArray();
        int parameterCount = captureRun.Commands.Sum(command => command.Parameters.Count);

        if (!shapeIds.SequenceEqual(expectedShapeIds))
        {
            throw new BaselinePlanValidationException(
                $"Query-shape inventory drifted. Expected [{string.Join(", ", expectedShapeIds)}], " +
                $"actual [{string.Join(", ", shapeIds)}].");
        }

        if (!commandKeys.SequenceEqual(definition.CommandKeys))
        {
            throw new BaselinePlanValidationException(
                "Query command role/order inventory drifted from the generation contract.");
        }

        if (shapeIds.Length != definition.ShapeCount ||
            captureRun.Commands.Count != definition.CommandCount ||
            parameterCount != definition.TypedParameterCount ||
            (definition.GenerationId == QueryReviewGenerations.FourRootDiscoveryId &&
             !string.Equals(
                 captureRun.GenerationId,
                 definition.GenerationId,
                 StringComparison.Ordinal)) ||
            !string.Equals(captureRun.LogicalRunId, definition.LogicalRunId, StringComparison.Ordinal) ||
            !string.Equals(captureRun.ProfileVersion, definition.ProfileIdentity, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Query-shape manifest totals or generation/profile identity drifted. " +
                $"Shapes={shapeIds.Length}/{definition.ShapeCount}; " +
                $"commands={captureRun.Commands.Count}/{definition.CommandCount}; " +
                $"parameters={parameterCount}/{definition.TypedParameterCount}.");
        }
    }

    internal static string ComputeManifestSha256(QueryShapeManifestSnapshot snapshot)
    {
        return ComputeSha256(JsonSerializer.Serialize(
            snapshot,
            JsonArtifactOutput.SerializerOptions));
    }

    internal static string ComputeResultIdentitySha256(
        IReadOnlyList<QueryShapeResult> results)
    {
        return ComputeSha256(JsonSerializer.Serialize(
            CreateResultIdentities(results),
            JsonArtifactOutput.SerializerOptions));
    }

    internal static void ValidateSuccessorResultIdentity(
        IReadOnlyList<QueryShapeResult> results)
    {
        string actual = ComputeResultIdentitySha256(results);

        if (!string.Equals(
                actual,
                ExpectedSuccessorResultIdentitySha256,
                StringComparison.Ordinal))
        {
            throw new SqlCaptureValidationException(
                "Successor total-count, selected-ID, or order identity drifted. " +
                $"Expected {ExpectedSuccessorResultIdentitySha256}, actual {actual}.");
        }
    }

    internal static QueryCommandIdentity CreateCommandIdentity(CapturedCommand command)
    {
        return new QueryCommandIdentity(
            CreateCommandKey(command),
            command.ShapeId,
            command.ShapeSequence,
            command.CommandRole,
            ComputeSha256(NormalizeSql(command.CommandText)),
            ComputeSha256(JsonSerializer.Serialize(
                command.Parameters,
                JsonArtifactOutput.SerializerOptions)),
            command.Parameters.Count);
    }

    internal static IReadOnlyList<QueryShapeInputIdentity> CreateInputIdentities()
    {
        return QueryShapeDefinitions.GetCanonicalShapeInputs()
            .Select(pair =>
            {
                string canonical = JsonSerializer.Serialize(
                    pair.Input,
                    CanonicalInputSerializerOptions);
                return new QueryShapeInputIdentity(
                    pair.ShapeId,
                    canonical,
                    ComputeSha256(canonical));
            })
            .ToArray();
    }

    internal static void ValidateRecordedInputIdentities(
        IReadOnlyList<QueryShapeInputIdentity> recorded)
    {
        IReadOnlyList<QueryShapeInputIdentity> expected = CreateInputIdentities();

        if (!recorded.SequenceEqual(expected))
        {
            throw new BaselinePlanValidationException(
                "Recorded successor canonical query inputs do not match the exact 21-shape " +
                "generation contract.");
        }
    }

    internal static void ValidateCommandIdentity(
        QueryCommandIdentity expected,
        CapturedCommand actual)
    {
        QueryCommandIdentity observed = CreateCommandIdentity(actual);

        if (expected != observed)
        {
            throw new BaselinePlanValidationException(
                $"Command '{expected.CommandKey}' SQL, role/order, or typed parameter " +
                "identity drifted.");
        }
    }

    private static QueryShapeManifestSnapshot CreateSuccessorSnapshot(
        SqlCaptureRun captureRun,
        QueryShapeContractDefinition definition)
    {
        IReadOnlyList<QueryShapeInputIdentity> inputs = CreateInputIdentities();
        QueryShapeResultIdentity[] results = CreateResultIdentities(
            captureRun.ShapeResults);
        QueryCommandIdentity[] commands = captureRun.Commands
            .Select(CreateCommandIdentity)
            .ToArray();

        return new QueryShapeManifestSnapshot(
            SchemaVersion: 2,
            QueryReviewGenerations.FourRootDiscoveryId,
            QueryReviewGenerations.FourRootDiscoveryId,
            QueryShapeDefinitions.FourRootLogicalRunId,
            QueryShapeDefinitions.LegacyShapeIds,
            inputs,
            results,
            commands,
            definition.ShapeCount,
            definition.CommandCount,
            definition.TypedParameterCount,
            definition.PlanCount,
            definition.ProfileInvariantCount);
    }

    private static QueryShapeResultIdentity[] CreateResultIdentities(
        IReadOnlyList<QueryShapeResult> results)
    {
        return results
            .Select(result => new QueryShapeResultIdentity(
                result.ShapeId,
                result.ActualTotalCount,
                result.ActualItemCount,
                result.ResultIds,
                ComputeSha256(string.Join(
                    "\n",
                    result.ResultIds.Select(id => id.ToString("D"))))))
            .ToArray();
    }

    private static void ValidateEquivalenceAndEmptyContracts(
        QueryShapeManifestSnapshot snapshot)
    {
        AssertEquivalent(
            snapshot,
            QueryShapeDefinitions.CommercialOfficeFirstPage,
            QueryShapeDefinitions.CommercialOfficeRootFirstPage);
        AssertEquivalent(
            snapshot,
            QueryShapeDefinitions.LandBuildingPlotFirstPage,
            QueryShapeDefinitions.LandBuildingPlotRootFirstPage);

        QueryShapeResultIdentity agencyLand = snapshot.Results.Single(result =>
            result.ShapeId == QueryShapeDefinitions.AgencyLandAgriculturalDeepPage);
        QueryShapeResultIdentity acceptedAgencyLand = AcceptedAgencyLandDeepPageResult;

        if (agencyLand.TotalCount != acceptedAgencyLand.TotalCount ||
            agencyLand.ItemCount != acceptedAgencyLand.ItemCount ||
            !agencyLand.OrderedIds.SequenceEqual(acceptedAgencyLand.OrderedIds) ||
            !string.Equals(
                agencyLand.OrderedIdsSha256,
                acceptedAgencyLand.OrderedIdsSha256,
                StringComparison.Ordinal))
        {
            throw new SqlCaptureValidationException(
                "The agency AgriculturalLand page-4 result must retain 71 matches and its " +
                "accepted ordered 11-row deep page.");
        }

        QueryShapeResultIdentity crossFamily = snapshot.Results.Single(result =>
            result.ShapeId == QueryShapeDefinitions.CrossFamilySubtypesEmpty);

        if (crossFamily.TotalCount != 0 ||
            crossFamily.ItemCount != 0 ||
            crossFamily.OrderedIds.Count != 0)
        {
            throw new SqlCaptureValidationException(
                "The cross-family subtype shape must lock an empty zero-count result.");
        }
    }

    private static void AssertEquivalent(
        QueryShapeManifestSnapshot snapshot,
        string implicitRootShape,
        string explicitRootShape)
    {
        QueryShapeResultIdentity implicitResult = snapshot.Results.Single(result =>
            result.ShapeId == implicitRootShape);
        QueryShapeResultIdentity explicitResult = snapshot.Results.Single(result =>
            result.ShapeId == explicitRootShape);

        if (implicitResult.TotalCount != explicitResult.TotalCount ||
            implicitResult.ItemCount != explicitResult.ItemCount ||
            !implicitResult.OrderedIds.SequenceEqual(explicitResult.OrderedIds) ||
            !string.Equals(
                implicitResult.OrderedIdsSha256,
                explicitResult.OrderedIdsSha256,
                StringComparison.Ordinal))
        {
            throw new SqlCaptureValidationException(
                $"Matching-root equivalence failed for '{implicitRootShape}' and " +
                $"'{explicitRootShape}'.");
        }
    }

    private static IReadOnlyList<string> BuildExpectedCommandKeys(
        QueryReviewGenerationDefinition generation)
    {
        return QueryShapeDefinitions.GetExpectedCommandRoles(generation)
            .SelectMany(pair => pair.Value.Keys.Select((role, index) =>
                $"{pair.Key}-{index + 1:D2}-{role}"))
            .ToArray();
    }

    private static void ValidateMappedHistoricalCommands(
        IReadOnlyList<CapturedCommand> commands)
    {
        QueryShapeContractDefinition historical = GetDefinition(
            QueryReviewGenerations.FrozenHistorical);
        CapturedCommand[] mapped = commands.Take(historical.CommandCount).ToArray();
        int parameterCount = mapped.Sum(command => command.Parameters.Count);

        if (parameterCount != historical.TypedParameterCount)
        {
            throw new SqlCaptureValidationException(
                $"Mapped historical shapes must retain exactly {historical.TypedParameterCount} " +
                $"typed parameters; captured {parameterCount}.");
        }

        string repositoryRoot = QueryReviewGenerations.GetRepositoryRoot();
        string historicalSqlRoot = Path.Combine(
            repositoryRoot,
            "docs",
            "benchmarks",
            "chapter-10f",
            "evidence",
            "sql");

        for (int index = 0; index < mapped.Length; index++)
        {
            CapturedCommand command = mapped[index];
            string commandKey = historical.CommandKeys[index];
            string acceptedPath = Path.Combine(historicalSqlRoot, $"{commandKey}.sql");

            if (!File.Exists(acceptedPath))
            {
                throw new SqlCaptureValidationException(
                    $"Accepted historical SQL artifact '{acceptedPath}' is missing.");
            }

            string accepted = NormalizeText(File.ReadAllText(acceptedPath));
            string actual = NormalizeText(BuildCuratedSql(commandKey, command));
            string actualSha256 = ComputeSha256(actual);
            string expectedSha256 = AcceptedHistoricalMappingSqlSha256[commandKey];

            if (!string.Equals(expectedSha256, actualSha256, StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"Mapped historical command '{commandKey}' drifted from its accepted " +
                    $"Chapter 14 SQL identity. Expected {expectedSha256}, actual {actualSha256}.");
            }

            string[] acceptedLines = accepted.Split('\n');
            string[] actualLines = actual.Split('\n');
            int parameterHeaderLineCount = command.Parameters.Count + 1;

            if (!acceptedLines.Take(parameterHeaderLineCount)
                    .SequenceEqual(actualLines.Take(parameterHeaderLineCount)))
            {
                throw new SqlCaptureValidationException(
                    $"Mapped historical command '{commandKey}' typed parameter identity drifted " +
                    "from the accepted 80-parameter artifact.");
            }
        }
    }

    private static string BuildCuratedSql(string commandKey, CapturedCommand command)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"-- {commandKey}");

        foreach (CapturedParameter parameter in command.Parameters)
        {
            builder.Append("-- ")
                .Append(parameter.Name)
                .Append(": CLR=")
                .Append(parameter.ClrType)
                .Append(", DbType=")
                .Append(parameter.DbType)
                .Append(", NpgsqlDbType=")
                .Append(parameter.NpgsqlDbType ?? "n/a")
                .Append(", Nullable=")
                .Append(parameter.IsNullable)
                .Append(", Value=")
                .AppendLine(parameter.Value);
        }

        builder.AppendLine(command.CommandText);
        return builder.ToString();
    }

    private static string CreateCommandKey(CapturedCommand command) =>
        $"{command.ShapeId}-{command.ShapeSequence:D2}-{command.CommandRole}";

    private static string NormalizeSql(string sql) => NormalizeText(sql);

    private static string NormalizeText(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    private static string ComputeSha256(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
