namespace RealEstate.Application.Auth.Dtos;

public sealed record LoginResponse(
    string AccessToken,
    AuthUserResponse User);
