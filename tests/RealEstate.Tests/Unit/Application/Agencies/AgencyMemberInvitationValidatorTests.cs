using FluentAssertions;
using RealEstate.Application.Agencies.Commands.AcceptAgencyInvitation;
using RealEstate.Application.Agencies.Commands.ChangeAgencyMemberRole;
using RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Agencies;

public sealed class AgencyMemberInvitationValidatorTests
{
    [Theory]
    [InlineData(null, "request", "Request is required.")]
    [InlineData(AgencyMemberRole.Manager, "role", "Agency member role must be Owner or Agent.")]
    [InlineData((AgencyMemberRole)999, "role", "Agency member role must be Owner or Agent.")]
    public void ChangeRole_InvalidRequest_ReturnsExpectedKeyAndFirstError(
        AgencyMemberRole? role,
        string key,
        string error)
    {
        var validator = new ChangeAgencyMemberRoleValidator();
        ChangeAgencyMemberRoleRequest? request = role.HasValue
            ? new ChangeAgencyMemberRoleRequest { Role = role.Value }
            : null;

        ChangeAgencyMemberRoleValidator.ValidationFailure? result =
            validator.ValidateWithKey(request);

        result.Should().BeEquivalentTo(
            new ChangeAgencyMemberRoleValidator.ValidationFailure(key, error));
    }

    [Fact]
    public void ChangeRole_AssignableRoles_AreValid()
    {
        var validator = new ChangeAgencyMemberRoleValidator();

        validator.ValidateWithKey(
            new ChangeAgencyMemberRoleRequest { Role = AgencyMemberRole.Owner })
            .Should().BeNull();
        validator.ValidateWithKey(
            new ChangeAgencyMemberRoleRequest { Role = AgencyMemberRole.Agent })
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null, 0, "request", "Request is required.")]
    [InlineData("", (int)AgencyMemberRole.Agent, "email", "Invitation email is required.")]
    [InlineData("invalid", (int)AgencyMemberRole.Agent, "email", "Invitation email is invalid.")]
    [InlineData("valid@test.com", (int)AgencyMemberRole.Manager, "role", "Invitation role must be Owner or Agent.")]
    public void CreateInvitation_InvalidRequest_ReturnsExpectedKeyAndFirstError(
        string? email,
        int role,
        string key,
        string error)
    {
        var validator = new CreateAgencyInvitationValidator();
        CreateAgencyInvitationRequest? request = email is null
            ? null
            : new CreateAgencyInvitationRequest
            {
                Email = email,
                Role = (AgencyMemberRole)role
            };

        CreateAgencyInvitationValidator.ValidationFailure? result =
            validator.ValidateWithKey(request);

        result.Should().BeEquivalentTo(
            new CreateAgencyInvitationValidator.ValidationFailure(key, error));
    }

    [Fact]
    public void CreateInvitation_OverlongEmail_PrecedesRoleValidation()
    {
        var validator = new CreateAgencyInvitationValidator();
        var request = new CreateAgencyInvitationRequest
        {
            Email = $"{new string('a', 250)}@test.com",
            Role = AgencyMemberRole.Manager
        };

        validator.ValidateWithKey(request).Should().BeEquivalentTo(
            new CreateAgencyInvitationValidator.ValidationFailure(
                "email",
                "Invitation email cannot be longer than 254 characters."));
    }

    [Theory]
    [InlineData(null, "request", "Request is required.")]
    [InlineData("", "token", "Invitation token is required.")]
    [InlineData(" ", "token", "Invitation token is required.")]
    public void AcceptInvitation_InvalidRequest_ReturnsExpectedKeyAndFirstError(
        string? token,
        string key,
        string error)
    {
        var validator = new AcceptAgencyInvitationValidator();
        AcceptAgencyInvitationRequest? request = token is null
            ? null
            : new AcceptAgencyInvitationRequest { Token = token };

        AcceptAgencyInvitationValidator.ValidationFailure? result =
            validator.ValidateWithKey(request);

        result.Should().BeEquivalentTo(
            new AcceptAgencyInvitationValidator.ValidationFailure(key, error));
    }

    [Fact]
    public void AcceptInvitation_NonblankOpaqueToken_IsValid()
    {
        var validator = new AcceptAgencyInvitationValidator();

        validator.ValidateWithKey(
            new AcceptAgencyInvitationRequest { Token = "opaque-token" })
            .Should().BeNull();
    }
}
