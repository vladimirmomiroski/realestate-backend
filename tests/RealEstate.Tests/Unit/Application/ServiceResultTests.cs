using FluentAssertions;
using RealEstate.Application.Common;

namespace RealEstate.Tests.Unit.Application;

public sealed class ServiceResultTests
{
    [Fact]
    public void ExistingFactories_RemainCompatibleAndUncoded()
    {
        ServiceResult<string> success = ServiceResult<string>.Success("value");
        ServiceResult<string> validation =
            ServiceResult<string>.ValidationError("invalid");
        ServiceResult<string> notFound =
            ServiceResult<string>.NotFound("missing");
        ServiceResult<string> forbidden =
            ServiceResult<string>.Forbidden("forbidden");
        ServiceResult<string> unauthorized =
            ServiceResult<string>.Unauthorized("unauthorized");

        success.Status.Should().Be(ServiceResultStatus.Success);
        success.Value.Should().Be("value");
        success.ErrorCode.Should().BeNull();

        validation.Status.Should().Be(ServiceResultStatus.ValidationError);
        validation.Error.Should().Be("invalid");
        validation.ErrorCode.Should().BeNull();

        notFound.Status.Should().Be(ServiceResultStatus.NotFound);
        notFound.ErrorCode.Should().BeNull();

        forbidden.Status.Should().Be(ServiceResultStatus.Forbidden);
        forbidden.ErrorCode.Should().BeNull();

        unauthorized.Status.Should().Be(ServiceResultStatus.Unauthorized);
        unauthorized.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Conflict_CarriesStableErrorCode()
    {
        ServiceResult<string> result = ServiceResult<string>.Conflict(
            "The resource changed.",
            ErrorCodes.ConflictResourceState);

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.Value.Should().BeNull();
        result.Error.Should().Be("The resource changed.");
        result.ErrorCode.Should().Be(ErrorCodes.ConflictResourceState);
    }

    [Fact]
    public void ExistingFailureStatus_CanCarryStableErrorCode()
    {
        ServiceResult<string> result = ServiceResult<string>.NotFound(
            "The resource was not found.",
            ErrorCodes.ResourceNotFound);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public void CodedFactory_RejectsCodeOutsideClosedCatalogue()
    {
        Action act = () => ServiceResult<string>.Conflict(
            "Conflict.",
            "conflict.message_derived_value");

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("errorCode");
    }

    [Fact]
    public void ErrorCodeCatalogue_IsClosedAndCompleteForChapter12()
    {
        ErrorCodes.All.Should().BeEquivalentTo(
            new[]
            {
                "validation.failed",
                "validation.file_required",
                "validation.file_empty",
                "validation.file_too_large",
                "validation.file_type_not_supported",
                "authentication.required",
                "authentication.invalid_credentials",
                "authentication.invalid_principal",
                "authorization.forbidden",
                "authorization.account_disabled",
                "resource.not_found",
                "request.method_not_allowed",
                "request.media_type_not_supported",
                "conflict.email_already_exists",
                "conflict.agency_slug_already_exists",
                "conflict.resource_state",
                "conflict.resource_capacity",
                "conflict.resource_set_changed",
                "server.unexpected"
            });
    }
}
