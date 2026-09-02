using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding.Tokens;

namespace RealEstate.Infrastructure.Security;

public sealed class DataProtectionLocationConfirmationTokenProtector
    : ILocationConfirmationTokenProtector
{
    public const string Purpose =
        "RealEstate.Api.Listings.LocationConfirmation.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    public DataProtectionLocationConfirmationTokenProtector(
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        IOptions<LocationConfirmationTokenOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _timeProvider = timeProvider;
        _lifetime = options.Value.GetLifetime();
    }

    public string Protect(LocationConfirmationTokenClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        DateTimeOffset issuedAtUtc = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAtUtc = issuedAtUtc.Add(_lifetime);

        var serializedPayload = new SerializedPayload(
            LocationConfirmationTokenPayload.CurrentVersion,
            claims.ListingId,
            claims.ActorUserId,
            claims.ProviderKey,
            claims.ProviderResultReference,
            claims.LanguageCode,
            claims.LocationFingerprint,
            issuedAtUtc,
            expiresAtUtc);

        string json = JsonSerializer.Serialize(
            serializedPayload,
            SerializerOptions);

        return _protector.Protect(json);
    }

    public LocationConfirmationTokenUnprotectResult Unprotect(
        string protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
        {
            return Invalid();
        }

        string json;

        try
        {
            json = _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            return Invalid();
        }
        catch (FormatException)
        {
            return Invalid();
        }

        SerializedPayload? serializedPayload;

        try
        {
            serializedPayload = JsonSerializer.Deserialize<SerializedPayload>(
                json,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return Invalid();
        }
        catch (NotSupportedException)
        {
            return Invalid();
        }

        if (serializedPayload is null)
        {
            return Invalid();
        }

        if (serializedPayload.Version !=
            LocationConfirmationTokenPayload.CurrentVersion)
        {
            return LocationConfirmationTokenUnprotectResult.Failure(
                LocationConfirmationTokenUnprotectOutcome.UnsupportedVersion);
        }

        if (!LocationConfirmationTokenClaims.TryCreate(
                serializedPayload.ListingId,
                serializedPayload.ActorUserId,
                serializedPayload.ProviderKey,
                serializedPayload.ProviderResultReference,
                serializedPayload.LanguageCode,
                serializedPayload.LocationFingerprint,
                out LocationConfirmationTokenClaims? claims) ||
            !HasValidTimes(serializedPayload))
        {
            return Invalid();
        }

        if (_timeProvider.GetUtcNow() >= serializedPayload.ExpiresAtUtc)
        {
            return LocationConfirmationTokenUnprotectResult.Failure(
                LocationConfirmationTokenUnprotectOutcome.Expired);
        }

        LocationConfirmationTokenPayload payload =
            LocationConfirmationTokenPayload.CreateCurrent(
                claims!,
                serializedPayload.IssuedAtUtc,
                serializedPayload.ExpiresAtUtc);

        return LocationConfirmationTokenUnprotectResult.Success(payload);
    }

    private static bool HasValidTimes(SerializedPayload payload)
    {
        TimeSpan lifetime = payload.ExpiresAtUtc - payload.IssuedAtUtc;

        return payload.IssuedAtUtc.Offset == TimeSpan.Zero &&
               payload.ExpiresAtUtc.Offset == TimeSpan.Zero &&
               lifetime > TimeSpan.Zero &&
               lifetime <= TimeSpan.FromMinutes(
                   LocationConfirmationTokenOptions.MaximumLifetimeMinutes);
    }

    private static LocationConfirmationTokenUnprotectResult Invalid()
    {
        return LocationConfirmationTokenUnprotectResult.Failure(
            LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    private sealed record SerializedPayload(
        int Version,
        Guid ListingId,
        Guid ActorUserId,
        string? ProviderKey,
        string? ProviderResultReference,
        string? LanguageCode,
        string? LocationFingerprint,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc);
}
