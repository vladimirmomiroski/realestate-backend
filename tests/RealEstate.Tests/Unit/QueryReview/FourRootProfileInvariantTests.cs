using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class FourRootProfileInvariantTests
{
    [Fact]
    public void LockedPopulationArithmetic_IsExactAndComplete()
    {
        IReadOnlyDictionary<string, long> expected =
            FourRootProfileInvariants.ExpectedValues;

        expected["listings.total"].Should().Be(100_000);
        expected["translations.total"].Should().Be(200_000);
        expected["images.total"].Should().Be(60_000);
        expected["property_type.apartment"].Should().Be(40_000);
        expected["property_type.house"].Should().Be(30_000);
        expected["property_type.commercial"].Should().Be(20_000);
        expected["property_type.land"].Should().Be(10_000);
        expected["commercial_type.unknown"].Should().Be(8_000);
        expected["commercial_type.office"].Should().Be(6_000);
        expected["commercial_type.shop"].Should().Be(4_000);
        expected["commercial_type.other"].Should().Be(2_000);
        expected["land_type.unknown"].Should().Be(4_000);
        expected["land_type.buildingplot"].Should().Be(3_000);
        expected["land_type.agriculturalland"].Should().Be(2_000);
        expected["land_type.other"].Should().Be(1_000);
        expected["currency.eur"].Should().Be(33_334);
        expected["currency.usd"].Should().Be(33_333);
        expected["currency.mkd"].Should().Be(33_333);
    }

    [Theory]
    [InlineData("root.apartment", 40000)]
    [InlineData("root.house", 30000)]
    [InlineData("root.commercial", 20000)]
    [InlineData("root.land", 10000)]
    [InlineData("commercial.unknown", 8000)]
    [InlineData("commercial.office", 6000)]
    [InlineData("commercial.shop", 4000)]
    [InlineData("commercial.other", 2000)]
    [InlineData("land.unknown", 4000)]
    [InlineData("land.buildingplot", 3000)]
    [InlineData("land.agriculturalland", 2000)]
    [InlineData("land.other", 1000)]
    public void EveryRootAndSubtypeBand_HasLockedLifecycleOwnershipAndListingType(
        string prefix,
        long total)
    {
        IReadOnlyDictionary<string, long> expected =
            FourRootProfileInvariants.ExpectedValues;

        expected[$"{prefix}.status.active"].Should().Be(total * 70 / 100);
        foreach (string status in new[] { "draft", "archived", "reserved", "sold", "rented" })
        {
            expected[$"{prefix}.status.{status}"].Should().Be(total * 6 / 100);
        }

        expected[$"{prefix}.ownership.personal"].Should().Be(total / 2);
        expected[$"{prefix}.ownership.agency"].Should().Be(total / 2);
        expected[$"{prefix}.listing_type.sale"].Should().Be(total / 2);
        expected[$"{prefix}.listing_type.rent"].Should().Be(total / 2);
    }

    [Fact]
    public void InvariantManifest_IsExplicitAndStableWithinGeneration()
    {
        FourRootProfileInvariants.InvariantCount.Should().Be(179);
        FourRootProfileInvariants.InvariantManifestSha256.Should().Be(
            "bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7");
        FourRootProfileInvariants.ExpectedValues.Keys.Should().OnlyHaveUniqueItems();
        FourRootProfileInvariants.ExpectedValues.Should().ContainKey(
            "details.exactly_one_matching").WhoseValue.Should().Be(100_000);
        FourRootProfileInvariants.ExpectedValues.Should().ContainKey(
            "distribution.sequence_windows_with_all_roots").WhoseValue.Should().Be(100);
        FourRootProfileInvariants.ExpectedValues.Should().ContainKey(
            "protected.legacy_property_type_violations").WhoseValue.Should().Be(0);
    }
}
