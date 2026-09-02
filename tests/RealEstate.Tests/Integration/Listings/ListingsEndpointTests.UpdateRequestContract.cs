using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData("listingType")]
    [InlineData("propertyType")]
    [InlineData("price")]
    [InlineData("currency")]
    [InlineData("areaSquareMeters")]
    [InlineData("translations")]
    public void UpdateListingRequest_WhenRequiredTopLevelMemberIsOmitted_ThrowsJsonException(
        string memberName)
    {
        JsonObject json = CreateValidRequestJson();
        json.Remove(memberName).Should().BeTrue();

        Action deserialize = () => Deserialize(json);

        deserialize.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("languageCode")]
    [InlineData("title")]
    public void UpdateListingRequest_WhenRequiredTranslationMemberIsOmitted_ThrowsJsonException(
        string memberName)
    {
        JsonObject json = CreateValidRequestJson();
        JsonObject translation = json["translations"]![0]!.AsObject();
        translation.Remove(memberName).Should().BeTrue();

        Action deserialize = () => Deserialize(json);

        deserialize.Should().Throw<JsonException>();
    }

    [Fact]
    public void UpdateListingRequest_WhenNullableMembersAreOmitted_RepresentsThemAsNull()
    {
        UpdateListingRequest request = Deserialize(CreateValidRequestJson());
        UpdateListingTranslationRequest translation = request.Translations.Single();

        request.Rooms.Should().BeNull();
        request.Bathrooms.Should().BeNull();
        request.BalconyCount.Should().BeNull();
        request.ParkingSpaces.Should().BeNull();
        request.HasBasement.Should().BeNull();
        request.IsExchangePossible.Should().BeNull();
        request.YearRenovated.Should().BeNull();
        request.YearBuilt.Should().BeNull();
        request.HouseDetails.Should().BeNull();
        translation.Description.Should().BeNull();
        translation.AddressLine.Should().BeNull();
        translation.City.Should().BeNull();
        translation.Municipality.Should().BeNull();
        translation.Neighborhood.Should().BeNull();
    }

    [Fact]
    public void UpdateListingRequest_WhenNullableMembersAreExplicitNull_MatchesOmission()
    {
        JsonObject json = CreateValidRequestJson();
        string[] nullableRootMembers =
        [
            "rooms", "bathrooms", "balconyCount", "parkingSpaces",
            "hasBasement", "isExchangePossible", "yearRenovated", "yearBuilt",
            "houseDetails"
        ];
        foreach (string member in nullableRootMembers)
        {
            json[member] = null;
        }

        JsonObject translation = json["translations"]![0]!.AsObject();
        string[] nullableTranslationMembers =
        [
            "description", "addressLine", "city", "municipality", "neighborhood"
        ];
        foreach (string member in nullableTranslationMembers)
        {
            translation[member] = null;
        }

        UpdateListingRequest request = Deserialize(json);

        request.Rooms.Should().BeNull();
        request.Bathrooms.Should().BeNull();
        request.BalconyCount.Should().BeNull();
        request.ParkingSpaces.Should().BeNull();
        request.HasBasement.Should().BeNull();
        request.IsExchangePossible.Should().BeNull();
        request.YearRenovated.Should().BeNull();
        request.YearBuilt.Should().BeNull();
        request.HouseDetails.Should().BeNull();
        request.Translations.Single().Description.Should().BeNull();
        request.Translations.Single().AddressLine.Should().BeNull();
        request.Translations.Single().City.Should().BeNull();
        request.Translations.Single().Municipality.Should().BeNull();
        request.Translations.Single().Neighborhood.Should().BeNull();
    }

    [Fact]
    public void UpdateListingRequest_WhenOptionalEnumsAreOmitted_UsesUnknownDefaults()
    {
        UpdateListingRequest request = Deserialize(CreateValidRequestJson());

        request.HeatingType.Should().Be(HeatingType.Unknown);
        request.FurnishingStatus.Should().Be(FurnishingStatus.Unknown);
        request.Condition.Should().Be(PropertyCondition.Unknown);
        request.Orientation.Should().Be(Orientation.Unknown);
        request.ApartmentDetails!.ApartmentType.Should().Be(ApartmentType.Unknown);
    }

    [Fact]
    public void UpdateListingRequest_WhenDefinedEnumsAreSupplied_PreservesValues()
    {
        JsonObject json = CreateValidRequestJson();
        json["heatingType"] = "Gas";
        json["furnishingStatus"] = "Furnished";
        json["condition"] = "Good";
        json["orientation"] = "SouthEast";
        json["apartmentDetails"]!["apartmentType"] = "Standard";

        UpdateListingRequest request = Deserialize(json);

        request.HeatingType.Should().Be(HeatingType.Gas);
        request.FurnishingStatus.Should().Be(FurnishingStatus.Furnished);
        request.Condition.Should().Be(PropertyCondition.Good);
        request.Orientation.Should().Be(Orientation.SouthEast);
        request.ApartmentDetails!.ApartmentType.Should().Be(ApartmentType.Standard);
    }

    [Fact]
    public void UpdateListingRequest_WhenUndefinedEnumNumericIsSupplied_ValidatorRejectsIt()
    {
        JsonObject json = CreateValidRequestJson();
        json["heatingType"] = 999;
        UpdateListingRequest request = Deserialize(json);

        request.HeatingType.Should().Be((HeatingType)999);

        var validator = new UpdateListingValidator();
        UpdateListingValidator.ValidationFailure? failure =
            validator.ValidateWithKey(request);
        failure.Should().NotBeNull();
        failure!.Key.Should().Be("heatingType");
    }

    [Fact]
    public void UpdateListingRequest_ContainsOnlyCurrentEditableReplacementMembers()
    {
        string[] propertyNames = typeof(UpdateListingRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        propertyNames.Should().BeEquivalentTo(
        [
            "ListingType", "PropertyType", "Price", "Currency",
            "AreaSquareMeters", "Rooms", "Bathrooms", "BalconyCount",
            "ParkingSpaces", "HasBasement", "IsExchangePossible",
            "HeatingType", "FurnishingStatus", "Condition", "YearRenovated",
            "Orientation", "YearBuilt",
            "ApartmentDetails", "HouseDetails", "Translations"
        ]);

        typeof(UpdateListingTranslationRequest).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
            [
                "LanguageCode", "Title", "Description", "AddressLine",
                "City", "Municipality", "Neighborhood"
            ]);
    }

    private UpdateListingRequest Deserialize(JsonObject json)
    {
        JsonSerializerOptions options = _factory.Services
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value
            .JsonSerializerOptions;

        return JsonSerializer.Deserialize<UpdateListingRequest>(
                json.ToJsonString(),
                options)
            ?? throw new InvalidOperationException(
                "The update-listing request did not deserialize.");
    }

    private static JsonObject CreateValidRequestJson()
    {
        return JsonNode.Parse(
            """
            {
              "listingType": "Sale",
              "propertyType": "Apartment",
              "price": 120000,
              "currency": "EUR",
              "areaSquareMeters": 60,
              "apartmentDetails": {},
              "translations": [
                {
                  "languageCode": "en",
                  "title": "Valid title"
                }
              ]
            }
            """)!
            .AsObject();
    }
}
