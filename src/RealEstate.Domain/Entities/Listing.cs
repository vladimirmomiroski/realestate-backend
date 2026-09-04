using RealEstate.Domain.Enums;
using System.Text;
using RealEstate.Domain.Common;
using RealEstate.Domain.Listings;

namespace RealEstate.Domain.Entities;

public class Listing : IAuditableEntity
{
    public Guid Id { get; set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? AgencyId { get; private set; }

    public Agency? Agency { get; private set; }

    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public ListingStatus Status { get; private set; } = ListingStatus.Draft;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "EUR";

    public decimal AreaSquareMeters { get; set; }

    public decimal? Rooms { get; set; }

    public decimal? Bathrooms { get; set; }

    public int? BalconyCount { get; set; }

    public int? ParkingSpaces { get; set; }

    public bool? HasBasement { get; set; }

    public bool? IsExchangePossible { get; set; }

    public HeatingType HeatingType { get; set; } = HeatingType.Unknown;

    public FurnishingStatus FurnishingStatus { get; set; } = FurnishingStatus.Unknown;

    public PropertyCondition Condition { get; set; } = PropertyCondition.Unknown;

    public int? YearRenovated { get; set; }

    public Orientation Orientation { get; set; } = Orientation.Unknown;

    public int? YearBuilt { get; set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public LocationPrecision? LocationPrecision { get; private set; }

    public string? GeocodingProviderKey { get; private set; }

    public string? GeocodingResultReference { get; private set; }

    public string? GeocodedDisplayName { get; private set; }

    public DateTime? LocationConfirmedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }

    public ListingApartmentDetails? ApartmentDetails { get; set; }

    public ListingHouseDetails? HouseDetails { get; set; }

    public ListingCommercialDetails? CommercialDetails { get; set; }

    public ListingLandDetails? LandDetails { get; set; }

    public ICollection<ListingTranslation> Translations { get; set; } =
        new List<ListingTranslation>();

    public ICollection<ListingImage> Images { get; set; } =
        new List<ListingImage>();

    public void AssignAgency(Guid agencyId)
    {
        if (agencyId == Guid.Empty)
        {
            throw new ArgumentException("Agency id cannot be empty.", nameof(agencyId));
        }

        AgencyId = agencyId;
    }

    public void ClearAgency()
    {
        AgencyId = null;
    }

    public void AssignCreator(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        CreatedByUserId = userId;
    }

    public void ConfirmLocation(
        decimal latitude,
        decimal longitude,
        LocationPrecision precision,
        string geocodingProviderKey,
        string geocodingResultReference,
        string? geocodedDisplayName,
        DateTime confirmedAtUtc)
    {
        if (latitude < ListingLocationRules.MinimumLatitude ||
            latitude > ListingLocationRules.MaximumLatitude)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                $"Latitude must be between {ListingLocationRules.MinimumLatitude} and {ListingLocationRules.MaximumLatitude}.");
        }

        if (longitude < ListingLocationRules.MinimumLongitude ||
            longitude > ListingLocationRules.MaximumLongitude)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                $"Longitude must be between {ListingLocationRules.MinimumLongitude} and {ListingLocationRules.MaximumLongitude}.");
        }

        if (!Enum.IsDefined(precision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "Location precision must be a defined value.");
        }

        ValidateCanonicalRequiredText(
            geocodingProviderKey,
            ListingLocationRules.GeocodingProviderKeyMaxLength,
            nameof(geocodingProviderKey));
        ValidateCanonicalRequiredText(
            geocodingResultReference,
            ListingLocationRules.GeocodingResultReferenceMaxLength,
            nameof(geocodingResultReference));

        if (geocodedDisplayName is not null)
        {
            ValidateCanonicalRequiredText(
                geocodedDisplayName,
                ListingLocationRules.GeocodedDisplayNameMaxLength,
                nameof(geocodedDisplayName));
        }

        if (confirmedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Location confirmation time must be UTC.",
                nameof(confirmedAtUtc));
        }

        Latitude = latitude;
        Longitude = longitude;
        LocationPrecision = precision;
        GeocodingProviderKey = geocodingProviderKey;
        GeocodingResultReference = geocodingResultReference;
        GeocodedDisplayName = geocodedDisplayName;
        LocationConfirmedAtUtc = confirmedAtUtc;
    }

    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        LocationPrecision = null;
        GeocodingProviderKey = null;
        GeocodingResultReference = null;
        GeocodedDisplayName = null;
        LocationConfirmedAtUtc = null;
    }

    public ListingPublicationReadinessResult EvaluatePublicationReadiness()
    {
        if (Translations.Count == 0)
        {
            return ListingPublicationReadinessResult.FromViolations(
            [
                new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.MissingTranslation,
                    TranslationId: null)
            ]);
        }

        var violations = new List<ListingPublicationReadinessViolation>();

        foreach (ListingTranslation translation in Translations)
        {
            if (translation.LanguageCode is null ||
                !ListingTranslationRules.IsCanonicalLanguageCode(
                    translation.LanguageCode))
            {
                violations.Add(new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.InvalidLanguageCode,
                    translation.Id));
            }

            if (!IsTrimmedNonBlank(translation.Title))
            {
                violations.Add(new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.InvalidTitle,
                    translation.Id));
            }

            if (!IsTrimmedNonBlank(translation.City))
            {
                violations.Add(new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.InvalidCity,
                    translation.Id));
            }

            if (!IsTrimmedNonBlank(translation.Municipality))
            {
                violations.Add(new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.InvalidMunicipality,
                    translation.Id));
            }

            if (!IsTrimmedNonBlank(translation.AddressLine))
            {
                violations.Add(new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.InvalidAddressLine,
                    translation.Id));
            }

            if (!IsTrimmedNonBlank(translation.Description))
            {
                violations.Add(new ListingPublicationReadinessViolation(
                    ListingPublicationReadinessViolationCode.InvalidDescription,
                    translation.Id));
            }
        }

        if (IsUnresolvedLocation() || IsLegacyUnverifiedLocation())
        {
            violations.Add(new ListingPublicationReadinessViolation(
                ListingPublicationReadinessViolationCode.MissingConfirmedLocation,
                TranslationId: null));
        }
        else if (!HasValidConfirmedLocation())
        {
            violations.Add(new ListingPublicationReadinessViolation(
                ListingPublicationReadinessViolationCode.InvalidConfirmedLocation,
                TranslationId: null));
        }

        return violations.Count == 0
            ? ListingPublicationReadinessResult.Ready
            : ListingPublicationReadinessResult.FromViolations(violations);
    }

    public ListingPublicationReadinessResult Publish()
    {
        if (Status != ListingStatus.Draft && Status != ListingStatus.Active)
        {
            throw new InvalidOperationException("Only draft listings can be published.");
        }

        ListingPublicationReadinessResult readiness =
            EvaluatePublicationReadiness();

        if (!readiness.IsReady)
        {
            return readiness;
        }

        if (Status == ListingStatus.Draft)
        {
            Status = ListingStatus.Active;
        }

        return readiness;
    }

    public void Unpublish()
    {
        if (Status == ListingStatus.Draft)
        {
            return;
        }

        if (Status != ListingStatus.Active)
        {
            throw new InvalidOperationException("Only active listings can be unpublished.");
        }

        Status = ListingStatus.Draft;
    }

    public void Archive()
    {
        if (Status == ListingStatus.Archived)
        {
            return;
        }

        if (Status != ListingStatus.Draft && Status != ListingStatus.Active)
        {
            throw new InvalidOperationException("Only draft or active listings can be archived.");
        }

        Status = ListingStatus.Archived;
    }

    public decimal CalculatePricePerSquareMeter()
    {
        if (AreaSquareMeters <= 0)
        {
            return 0;
        }

        return Price / AreaSquareMeters;
    }

    private static bool IsTrimmedNonBlank(string? value)
    {
        return value is not null &&
               value.Length > 0 &&
               value == ListingTranslationRules.NormalizeRequiredText(value);
    }

    private bool IsUnresolvedLocation()
    {
        return Latitude is null &&
               Longitude is null &&
               LocationPrecision is null &&
               GeocodingProviderKey is null &&
               GeocodingResultReference is null &&
               GeocodedDisplayName is null &&
               LocationConfirmedAtUtc is null;
    }

    private bool IsLegacyUnverifiedLocation()
    {
        return Latitude.HasValue &&
               Longitude.HasValue &&
               LocationPrecision is null &&
               GeocodingProviderKey is null &&
               GeocodingResultReference is null &&
               GeocodedDisplayName is null &&
               LocationConfirmedAtUtc is null;
    }

    private bool HasValidConfirmedLocation()
    {
        return Latitude is >= ListingLocationRules.MinimumLatitude and
                   <= ListingLocationRules.MaximumLatitude &&
               Longitude is >= ListingLocationRules.MinimumLongitude and
                   <= ListingLocationRules.MaximumLongitude &&
               LocationPrecision.HasValue &&
               Enum.IsDefined(LocationPrecision.Value) &&
               IsCanonicalRequiredText(
                   GeocodingProviderKey,
                   ListingLocationRules.GeocodingProviderKeyMaxLength) &&
               IsCanonicalRequiredText(
                   GeocodingResultReference,
                   ListingLocationRules.GeocodingResultReferenceMaxLength) &&
               (GeocodedDisplayName is null ||
                IsCanonicalRequiredText(
                    GeocodedDisplayName,
                    ListingLocationRules.GeocodedDisplayNameMaxLength)) &&
               LocationConfirmedAtUtc.HasValue;
    }

    private static bool IsCanonicalRequiredText(
        string? value,
        int maximumLength)
    {
        return value is not null &&
               value.Length > 0 &&
               value == ListingTranslationRules.NormalizeRequiredText(value) &&
               value.EnumerateRunes().Count() <= maximumLength;
    }

    private static void ValidateCanonicalRequiredText(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (value is null ||
            value.Length == 0 ||
            value != ListingTranslationRules.NormalizeRequiredText(value))
        {
            throw new ArgumentException(
                "Value must be nonblank and free of boundary whitespace.",
                parameterName);
        }

        int scalarLength = value.EnumerateRunes().Count();

        if (scalarLength > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                scalarLength,
                $"Value cannot exceed {maximumLength} characters.");
        }
    }
}
