using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Infrastructure.Security;

namespace RealEstate.Tests.Unit.Infrastructure.Security;

public sealed class DataProtectionLocationConfirmationTokenProtectorTests
{
    private static readonly DateTimeOffset InitialUtc =
        new(2026, 8, 17, 10, 15, 30, TimeSpan.Zero);

    [Fact]
    public void ProtectAndUnprotect_RoundTripsAllClaimsAndControlledTimes()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        var timeProvider = new ManualTimeProvider(InitialUtc);
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(keyRing.Path, "test-app", timeProvider);
        LocationConfirmationTokenClaims claims = CreateClaims();

        string token = protector.Protect(claims);
        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(token);

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain(claims.ProviderResultReference);
        token.Should().NotContain(claims.LocationFingerprint);
        result.Outcome.Should().Be(
            LocationConfirmationTokenUnprotectOutcome.Success);
        result.Succeeded.Should().BeTrue();
        result.Payload.Should().NotBeNull();
        result.Payload!.Version.Should().Be(
            LocationConfirmationTokenPayload.CurrentVersion);
        result.Payload.ListingId.Should().Be(claims.ListingId);
        result.Payload.ActorUserId.Should().Be(claims.ActorUserId);
        result.Payload.ProviderKey.Should().Be(claims.ProviderKey);
        result.Payload.ProviderResultReference.Should().Be(
            claims.ProviderResultReference);
        result.Payload.LanguageCode.Should().Be(claims.LanguageCode);
        result.Payload.LocationFingerprint.Should().Be(
            claims.LocationFingerprint);
        result.Payload.IssuedAtUtc.Should().Be(InitialUtc);
        result.Payload.ExpiresAtUtc.Should().Be(
            InitialUtc.AddMinutes(10));
    }

    [Fact]
    public void Unprotect_WhenTokenIsTampered_ReturnsInvalidWithoutPayload()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(keyRing.Path, "test-app");
        string token = protector.Protect(CreateClaims());
        char replacement = token[^1] == 'A' ? 'B' : 'A';
        string tampered = token[..^1] + replacement;

        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(tampered);

        AssertFailure(result, LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    [Fact]
    public void Unprotect_WhenTokenIsTruncated_ReturnsInvalidWithoutPayload()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(keyRing.Path, "test-app");
        string token = protector.Protect(CreateClaims());

        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(token[..(token.Length / 2)]);

        AssertFailure(result, LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-protected-token")]
    public void Unprotect_WhenTokenIsMalformed_ReturnsInvalid(
        string token)
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(keyRing.Path, "test-app");

        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(token);

        AssertFailure(result, LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    [Fact]
    public void Unprotect_UsesExplicitExpirationBoundaryWithoutSleeping()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        var timeProvider = new ManualTimeProvider(InitialUtc);
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(keyRing.Path, "test-app", timeProvider);
        string token = protector.Protect(CreateClaims());

        timeProvider.SetUtcNow(InitialUtc.AddMinutes(10).AddTicks(-1));
        protector.Unprotect(token).Outcome.Should().Be(
            LocationConfirmationTokenUnprotectOutcome.Success);

        timeProvider.SetUtcNow(InitialUtc.AddMinutes(10));
        LocationConfirmationTokenUnprotectResult atBoundary =
            protector.Unprotect(token);

        AssertFailure(
            atBoundary,
            LocationConfirmationTokenUnprotectOutcome.Expired);
    }

    [Fact]
    public void Unprotect_WhenPayloadVersionIsUnsupported_ReturnsTypedFailure()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        IDataProtectionProvider provider = CreateProvider(
            keyRing.Path,
            "test-app");
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(provider, new ManualTimeProvider(InitialUtc));
        LocationConfirmationTokenClaims claims = CreateClaims();
        string json = JsonSerializer.Serialize(new
        {
            version = 99,
            listingId = claims.ListingId,
            actorUserId = claims.ActorUserId,
            providerKey = claims.ProviderKey,
            providerResultReference = claims.ProviderResultReference,
            languageCode = claims.LanguageCode,
            locationFingerprint = claims.LocationFingerprint,
            issuedAtUtc = InitialUtc,
            expiresAtUtc = InitialUtc.AddMinutes(10)
        });
        string token = provider
            .CreateProtector(
                DataProtectionLocationConfirmationTokenProtector.Purpose)
            .Protect(json);

        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(token);

        AssertFailure(
            result,
            LocationConfirmationTokenUnprotectOutcome.UnsupportedVersion);
    }

    [Fact]
    public void Unprotect_WhenProtectedPayloadIsStructurallyInvalid_ReturnsInvalid()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        IDataProtectionProvider provider = CreateProvider(
            keyRing.Path,
            "test-app");
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(provider, new ManualTimeProvider(InitialUtc));
        LocationConfirmationTokenClaims claims = CreateClaims();
        string json = JsonSerializer.Serialize(new
        {
            version = LocationConfirmationTokenPayload.CurrentVersion,
            claims.ListingId,
            claims.ActorUserId,
            claims.ProviderKey,
            claims.ProviderResultReference,
            claims.LanguageCode,
            locationFingerprint = "malformed",
            issuedAtUtc = InitialUtc,
            expiresAtUtc = InitialUtc.AddMinutes(10)
        });
        string token = provider
            .CreateProtector(
                DataProtectionLocationConfirmationTokenProtector.Purpose)
            .Protect(json);

        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(token);

        AssertFailure(result, LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    [Fact]
    public void PurposeIsolation_DifferentPurposeCannotUnprotectToken()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        IDataProtectionProvider provider = CreateProvider(
            keyRing.Path,
            "test-app");
        DataProtectionLocationConfirmationTokenProtector protector =
            CreateProtector(provider, new ManualTimeProvider(InitialUtc));
        string token = protector.Protect(CreateClaims());

        Action act = () => provider
            .CreateProtector("RealEstate.Api.DifferentPurpose.v1")
            .Unprotect(token);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void SharedRingAndApplicationDiscriminator_InteroperateAcrossInstances()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        DataProtectionLocationConfirmationTokenProtector instanceA =
            CreateProtector(keyRing.Path, "shared-app");
        string token = instanceA.Protect(CreateClaims());

        DataProtectionLocationConfirmationTokenProtector instanceB =
            CreateProtector(keyRing.Path, "shared-app");
        LocationConfirmationTokenUnprotectResult result =
            instanceB.Unprotect(token);

        result.Outcome.Should().Be(
            LocationConfirmationTokenUnprotectOutcome.Success);
    }

    [Fact]
    public void DifferentApplicationDiscriminator_DoesNotInteroperate()
    {
        using TestKeyRing keyRing = TestKeyRing.Create();
        DataProtectionLocationConfirmationTokenProtector source =
            CreateProtector(keyRing.Path, "application-a");
        string token = source.Protect(CreateClaims());
        DataProtectionLocationConfirmationTokenProtector isolated =
            CreateProtector(keyRing.Path, "application-b");

        LocationConfirmationTokenUnprotectResult result =
            isolated.Unprotect(token);

        AssertFailure(result, LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    [Fact]
    public void DifferentKeyRing_DoesNotInteroperate()
    {
        using TestKeyRing firstRing = TestKeyRing.Create();
        using TestKeyRing secondRing = TestKeyRing.Create();
        DataProtectionLocationConfirmationTokenProtector source =
            CreateProtector(firstRing.Path, "shared-app");
        string token = source.Protect(CreateClaims());
        DataProtectionLocationConfirmationTokenProtector isolated =
            CreateProtector(secondRing.Path, "shared-app");

        LocationConfirmationTokenUnprotectResult result =
            isolated.Unprotect(token);

        AssertFailure(result, LocationConfirmationTokenUnprotectOutcome.Invalid);
    }

    private static DataProtectionLocationConfirmationTokenProtector
        CreateProtector(
            string keyRingPath,
            string applicationName,
            TimeProvider? timeProvider = null)
    {
        return CreateProtector(
            CreateProvider(keyRingPath, applicationName),
            timeProvider ?? new ManualTimeProvider(InitialUtc));
    }

    private static DataProtectionLocationConfirmationTokenProtector
        CreateProtector(
            IDataProtectionProvider provider,
            TimeProvider timeProvider)
    {
        return new DataProtectionLocationConfirmationTokenProtector(
            provider,
            timeProvider,
            Options.Create(
                new LocationConfirmationTokenOptions
                {
                    LifetimeMinutes = 10
                }));
    }

    private static IDataProtectionProvider CreateProvider(
        string keyRingPath,
        string applicationName)
    {
        return DataProtectionProvider.Create(
            new DirectoryInfo(keyRingPath),
            builder => builder.SetApplicationName(applicationName));
    }

    private static LocationConfirmationTokenClaims CreateClaims()
    {
        string fingerprint = ListingLocationFingerprint.Compute(
            CanonicalListingLocation.From(
            [
                new CanonicalListingLocationInput(
                    "mk",
                    "Скопје",
                    "Центар",
                    "Македонија 10",
                    "Дебар Маало")
            ]));

        return LocationConfirmationTokenClaims.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "provider",
            "opaque/reference:AbC-123_\U0001F3E0",
            "mk",
            fingerprint);
    }

    private static void AssertFailure(
        LocationConfirmationTokenUnprotectResult result,
        LocationConfirmationTokenUnprotectOutcome outcome)
    {
        result.Outcome.Should().Be(outcome);
        result.Succeeded.Should().BeFalse();
        result.Payload.Should().BeNull();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }

    private sealed class TestKeyRing : IDisposable
    {
        private TestKeyRing(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestKeyRing Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "RealEstate.Tests",
                nameof(DataProtectionLocationConfirmationTokenProtectorTests),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TestKeyRing(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
