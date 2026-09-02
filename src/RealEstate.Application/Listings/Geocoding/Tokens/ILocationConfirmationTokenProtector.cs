namespace RealEstate.Application.Listings.Geocoding.Tokens;

public interface ILocationConfirmationTokenProtector
{
    string Protect(LocationConfirmationTokenClaims claims);

    LocationConfirmationTokenUnprotectResult Unprotect(
        string protectedToken);
}
