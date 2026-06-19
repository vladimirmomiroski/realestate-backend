namespace RealEstate.Application.Auth.Commands.LoginUser;

public sealed record LoginRequest(
    string Email,
    string Password);