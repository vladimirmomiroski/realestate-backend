using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlTranslationOrderingTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlTranslationOrderingTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostgreSql_CCollation_ShouldOrderAdversarialLanguageCodesByUtf8Bytes()
    {
        // Arrange
        string[] values =
        [
            "\U00010000",
            "\u00E4",
            "aa",
            "\uE000",
            "A",
            "z",
            "a\u0308",
            "a"
        ];

        string[] expected =
        [
            "A",
            "a",
            "aa",
            "a\u0308",
            "z",
            "\u00E4",
            "\uE000",
            "\U00010000"
        ];

        // Act
        IReadOnlyList<string> ordered =
            await QueryOrderedStringsAsync(values);

        // Assert
        ordered.Should().Equal(expected);
    }

    [Fact]
    public async Task PostgreSql_Uuid_ShouldOrderAdversarialValuesByCanonicalFieldOrder()
    {
        // Arrange
        Guid[] values =
        [
            Guid.Parse("01000000-0000-0000-0000-000000000000"),
            Guid.Parse("00000000-0100-0000-0000-000000000000"),
            Guid.Parse("00000000-0000-0100-0000-000000000000"),
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            Guid.Parse("00000000-0001-0000-0000-000000000000"),
            Guid.Parse("00000000-0000-0001-0000-000000000000")
        ];

        Guid[] expected =
        [
            Guid.Parse("00000000-0000-0001-0000-000000000000"),
            Guid.Parse("00000000-0000-0100-0000-000000000000"),
            Guid.Parse("00000000-0001-0000-0000-000000000000"),
            Guid.Parse("00000000-0100-0000-0000-000000000000"),
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            Guid.Parse("01000000-0000-0000-0000-000000000000")
        ];

        // Act
        IReadOnlyList<Guid> ordered =
            await QueryOrderedGuidsAsync(values);

        // Assert
        ordered.Should().Equal(expected);
    }

    private async Task<IReadOnlyList<string>> QueryOrderedStringsAsync(
        IReadOnlyList<string> values)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        DbConnection connection =
            dbContext.Database.GetDbConnection();

        await dbContext.Database.OpenConnectionAsync();

        try
        {
            await using DbCommand command =
                connection.CreateCommand();

            string valueRows = AddParameters(
                command,
                values.Cast<object>().ToArray());

            command.CommandText =
                $"""
                 SELECT probe."Value"
                 FROM (
                     VALUES {valueRows}
                 ) AS probe("Value")
                 ORDER BY probe."Value" COLLATE "C" ASC
                 """;

            var ordered = new List<string>();

            await using DbDataReader reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ordered.Add(reader.GetString(0));
            }

            return ordered;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<IReadOnlyList<Guid>> QueryOrderedGuidsAsync(
        IReadOnlyList<Guid> values)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        DbConnection connection =
            dbContext.Database.GetDbConnection();

        await dbContext.Database.OpenConnectionAsync();

        try
        {
            await using DbCommand command =
                connection.CreateCommand();

            string valueRows = AddParameters(
                command,
                values.Cast<object>().ToArray());

            command.CommandText =
                $"""
                 SELECT probe."Value"
                 FROM (
                     VALUES {valueRows}
                 ) AS probe("Value")
                 ORDER BY probe."Value" ASC
                 """;

            var ordered = new List<Guid>();

            await using DbDataReader reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ordered.Add(reader.GetGuid(0));
            }

            return ordered;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static string AddParameters(
        DbCommand command,
        IReadOnlyList<object> values)
    {
        var rows = new string[values.Count];

        for (int index = 0; index < values.Count; index++)
        {
            string parameterName = $"p{index}";

            DbParameter parameter =
                command.CreateParameter();

            parameter.ParameterName = parameterName;
            parameter.Value = values[index];

            command.Parameters.Add(parameter);

            rows[index] = $"(@{parameterName})";
        }

        return string.Join(", ", rows);
    }
}
