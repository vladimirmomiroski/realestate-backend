using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly CreateListingValidator _validator;

    public CreateListingHandler(
        IListingRepository listingRepository,
        CreateListingValidator validator)
    {
        _listingRepository = listingRepository;
        _validator = validator;
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
            Floor = request.Floor,
            TotalFloors = request.TotalFloors,
            YearBuilt = request.YearBuilt,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CreatedAtUtc = DateTime.UtcNow,
            Translations = request.Translations.Select(translation => new ListingTranslation
            {
                Id = Guid.NewGuid(),
                LanguageCode = NormalizeLanguageCode(translation.LanguageCode),
                Title = translation.Title.Trim(),
                Description = CleanNullableText(translation.Description),
                AddressLine = CleanNullableText(translation.AddressLine),
                City = CleanNullableText(translation.City),
                Neighborhood = CleanNullableText(translation.Neighborhood)
            }).ToList()
        };

        await _listingRepository.CreateAsync(listing, cancellationToken);

        var response = listing.ToResponse("mk");

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
