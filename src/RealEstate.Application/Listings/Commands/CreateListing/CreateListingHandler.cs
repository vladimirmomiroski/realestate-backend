using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingHandler
{

    private const int MaxFreeListingsPerUser = 3;

    private readonly IListingRepository _listingRepository;
    private readonly CreateListingValidator _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAgencyRepository _agencyRepository;

    public CreateListingHandler(
        IListingRepository listingRepository,
        IAgencyRepository agencyRepository,
        CreateListingValidator validator,
        ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _agencyRepository = agencyRepository;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<ListingResponse>> HandleAsync(
    CreateListingRequest request,
    CancellationToken cancellationToken)
    {
        var validationError = _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<ListingResponse>.ValidationError(validationError);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        int existingListingsCount =
            await _listingRepository.CountByCreatedByUserIdAsync(
                userId,
                cancellationToken);

        if (existingListingsCount >= MaxFreeListingsPerUser)
        {
            return ServiceResult<ListingResponse>.ValidationError(
                "Free listing limit reached. Each user can create up to 3 listings.");
        }

        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            ListingType = request.ListingType,
            PropertyType = request.PropertyType,
            Status = ListingStatus.Draft,
            Price = request.Price,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            AreaSquareMeters = request.AreaSquareMeters,
            Rooms = request.Rooms,
            Bathrooms = request.Bathrooms,
            BalconyCount = request.BalconyCount,
            ParkingSpaces = request.ParkingSpaces,
            HasBasement = request.HasBasement,
            IsExchangePossible = request.IsExchangePossible,
            HeatingType = request.HeatingType,
            FurnishingStatus = request.FurnishingStatus,
            Condition = request.Condition,
            YearRenovated = request.YearRenovated,
            Orientation = request.Orientation,
            YearBuilt = request.YearBuilt,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Translations = request.Translations.Select(translation => new ListingTranslation
            {
                Id = Guid.NewGuid(),
                LanguageCode = NormalizeLanguageCode(translation.LanguageCode),
                Title = translation.Title.Trim(),
                Description = CleanNullableText(translation.Description),
                AddressLine = CleanNullableText(translation.AddressLine),
                City = CleanNullableText(translation.City),
                Municipality = CleanNullableText(translation.Municipality),
                Neighborhood = CleanNullableText(translation.Neighborhood)
            }).ToList()
        };

        listing.AssignCreator(userId);

        if (request.AgencyId.HasValue)
        {
            bool agencyExists = await _agencyRepository.ExistsAsync(
                request.AgencyId.Value,
                cancellationToken);

            if (!agencyExists)
            {
                return ServiceResult<ListingResponse>.NotFound("Agency was not found.");
            }

            bool isActiveMember = await _agencyRepository.IsActiveMemberAsync(
                request.AgencyId.Value,
                userId,
                cancellationToken);

            if (!isActiveMember)
            {
                return ServiceResult<ListingResponse>.Forbidden(
                    "User is not an active member of this agency.");
            }

            listing.AssignAgency(request.AgencyId.Value);
        }

        if (request.PropertyType == PropertyType.Apartment &&
            request.ApartmentDetails is not null)
        {
            listing.ApartmentDetails = new ListingApartmentDetails
            {
                ListingId = listing.Id,
                ApartmentType = request.ApartmentDetails.ApartmentType,
                Floor = request.ApartmentDetails.Floor,
                TotalFloors = request.ApartmentDetails.TotalFloors,
                HasElevator = request.ApartmentDetails.HasElevator
            };
        }

        if (request.PropertyType == PropertyType.House &&
            request.HouseDetails is not null)
        {
            listing.HouseDetails = new ListingHouseDetails
            {
                ListingId = listing.Id,
                HouseType = request.HouseDetails.HouseType,
                NumberOfFloors = request.HouseDetails.NumberOfFloors,
                YardAreaSquareMeters = request.HouseDetails.YardAreaSquareMeters
            };
        }

        await _listingRepository.CreateAsync(listing, cancellationToken);

        var preferredLanguageCode = NormalizeLanguageCode(request.Translations.First().LanguageCode);

        var response = listing.ToResponse(preferredLanguageCode);

        return ServiceResult<ListingResponse>.Success(response);
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        return languageCode.Trim().ToLowerInvariant();
    }

    private static string? CleanNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
