using FluentAssertions;
using RealEstate.Application.Agencies.Commands.CreateAgency;
using RealEstate.Application.Agencies.Commands.UpdateAgency;
using RealEstate.Application.Agencies.Dtos;

namespace RealEstate.Tests.Unit.Application.Agencies;

public sealed class AgencyValidatorTests
{
    public static TheoryData<Action<CreateAgencyRequest>, string> CreateFailures => new()
    {
        { request => request.Name = "", "name" },
        { request => request.Name = new string('a', 151), "name" },
        { request => request.Slug = "", "slug" },
        { request => request.Slug = "ab", "slug" },
        { request => request.Slug = new string('a', 101), "slug" },
        { request => request.Slug = "invalid_slug", "slug" },
        { request => request.Description = new string('a', 1001), "description" },
        { request => request.PhoneNumber = new string('1', 51), "phoneNumber" },
        { request => request.Email = new string('a', 255), "email" },
        { request => request.Email = "invalid", "email" },
        { request => request.WebsiteUrl = new string('a', 501), "websiteUrl" },
        { request => request.WebsiteUrl = "relative", "websiteUrl" },
        { request => request.AddressLine = new string('a', 251), "addressLine" },
        { request => request.City = new string('a', 101), "city" },
        { request => request.Municipality = new string('a', 101), "municipality" }
    };

    public static TheoryData<Action<UpdateAgencyRequest>, string> UpdateFailures => new()
    {
        { request => request.Name = "", "name" },
        { request => request.Name = new string('a', 151), "name" },
        { request => request.Description = new string('a', 1001), "description" },
        { request => request.PhoneNumber = new string('1', 51), "phoneNumber" },
        { request => request.Email = new string('a', 255), "email" },
        { request => request.Email = "invalid", "email" },
        { request => request.WebsiteUrl = new string('a', 501), "websiteUrl" },
        { request => request.WebsiteUrl = "relative", "websiteUrl" },
        { request => request.AddressLine = new string('a', 251), "addressLine" },
        { request => request.City = new string('a', 101), "city" },
        { request => request.Municipality = new string('a', 101), "municipality" }
    };

    [Theory]
    [MemberData(nameof(CreateFailures))]
    public void CreateValidator_ReturnsStableJsonFacingKey(
        Action<CreateAgencyRequest> mutate,
        string expectedKey)
    {
        var request = ValidCreateRequest();
        mutate(request);

        CreateAgencyValidator.ValidationFailure? failure =
            new CreateAgencyValidator().ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(expectedKey);
        failure.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(UpdateFailures))]
    public void UpdateValidator_ReturnsStableJsonFacingKey(
        Action<UpdateAgencyRequest> mutate,
        string expectedKey)
    {
        var request = ValidUpdateRequest();
        mutate(request);

        UpdateAgencyValidator.ValidationFailure? failure =
            new UpdateAgencyValidator().ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(expectedKey);
        failure.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validators_UseRequestForNullAndPreserveFirstErrorOrdering()
    {
        var createValidator = new CreateAgencyValidator();
        createValidator.ValidateWithKey(null!).Should().BeEquivalentTo(
            new CreateAgencyValidator.ValidationFailure(
                "request",
                "Request is required."));

        var invalidCreate = ValidCreateRequest();
        invalidCreate.Name = "";
        invalidCreate.Slug = "";
        createValidator.ValidateWithKey(invalidCreate)!.Key.Should().Be("name");

        var updateValidator = new UpdateAgencyValidator();
        updateValidator.ValidateWithKey(null!).Should().BeEquivalentTo(
            new UpdateAgencyValidator.ValidationFailure(
                "request",
                "Request is required."));

        var invalidUpdate = ValidUpdateRequest();
        invalidUpdate.Name = "";
        invalidUpdate.Email = "invalid";
        updateValidator.ValidateWithKey(invalidUpdate)!.Key.Should().Be("name");
    }

    private static CreateAgencyRequest ValidCreateRequest() => new()
    {
        Name = "Agency",
        Slug = "agency-slug",
        Email = "agency@test.com",
        WebsiteUrl = "https://agency.test"
    };

    private static UpdateAgencyRequest ValidUpdateRequest() => new()
    {
        Name = "Agency",
        Email = "agency@test.com",
        WebsiteUrl = "https://agency.test"
    };
}
