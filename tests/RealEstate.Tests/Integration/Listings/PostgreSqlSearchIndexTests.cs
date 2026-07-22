using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlSearchIndexTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlSearchIndexTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListingTranslationQIndex_UsesExpectedTrigramGinDefinition()
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

            command.CommandText =
                """
                SELECT
                    EXISTS (
                        SELECT 1
                        FROM pg_extension
                        WHERE extname = 'pg_trgm'
                    ) AS "ExtensionInstalled",
                    access_method.amname AS "AccessMethod",
                    index_metadata.indisvalid AS "IsValid",
                    index_metadata.indisready AS "IsReady",
                    array_agg(table_column.attname::text ORDER BY index_key.ordinality) AS "Columns",
                    array_agg(operator_class.opcname::text ORDER BY index_key.ordinality) AS "OperatorClasses"
                FROM pg_class index_relation
                JOIN pg_index index_metadata
                    ON index_metadata.indexrelid = index_relation.oid
                JOIN pg_class table_relation
                    ON table_relation.oid = index_metadata.indrelid
                JOIN pg_am access_method
                    ON access_method.oid = index_relation.relam
                CROSS JOIN LATERAL unnest(
                    index_metadata.indkey::smallint[],
                    index_metadata.indclass::oid[]
                ) WITH ORDINALITY AS index_key(attnum, opclass_oid, ordinality)
                JOIN pg_attribute table_column
                    ON table_column.attrelid = table_relation.oid
                    AND table_column.attnum = index_key.attnum
                JOIN pg_opclass operator_class
                    ON operator_class.oid = index_key.opclass_oid
                WHERE table_relation.relname = 'ListingTranslations'
                    AND index_relation.relname = 'IX_ListingTranslations_Q_Trigram'
                GROUP BY
                    access_method.amname,
                    index_metadata.indisvalid,
                    index_metadata.indisready
                """;

            await using DbDataReader reader =
                await command.ExecuteReaderAsync();

            bool indexExists = await reader.ReadAsync();

            indexExists.Should().BeTrue();
            reader.GetBoolean(0).Should().BeTrue();
            reader.GetString(1).Should().Be("gin");
            reader.GetBoolean(2).Should().BeTrue();
            reader.GetBoolean(3).Should().BeTrue();
            reader.GetFieldValue<string[]>(4).Should().Equal(
                "Title",
                "City",
                "Municipality",
                "Neighborhood");
            reader.GetFieldValue<string[]>(5).Should().Equal(
                "gin_trgm_ops",
                "gin_trgm_ops",
                "gin_trgm_ops",
                "gin_trgm_ops");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
