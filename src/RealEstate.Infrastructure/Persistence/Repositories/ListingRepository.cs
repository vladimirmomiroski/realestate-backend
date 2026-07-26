using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Data;
using System.Data.Common;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository : IListingRepository
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const string PostgreSqlBytewiseCollation = "C";
    private const string LikeEscapeCharacter = "\\";

    private readonly RealEstateDbContext _dbContext;

    public ListingRepository(RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(Listing listing, CancellationToken cancellationToken)
    {
        _dbContext.Listings.Add(listing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
        GetListingsQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Listing> listingsQuery = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Active);

        listingsQuery = ApplyBasicFilters(listingsQuery, query);
        listingsQuery = ApplyPropertyDetailFilters(listingsQuery, query);
        listingsQuery =
            ApplyEffectiveTranslationFilters(
                listingsQuery,
                query);

        (int page, int pageSize) = NormalizePagination(query.Page, query.PageSize);

        int totalCount = await listingsQuery.CountAsync(cancellationToken);

        IOrderedQueryable<Listing> orderedQuery = ApplyOrdering(
            listingsQuery,
            query.SortOption);

        List<Listing> listings = await orderedQuery
            .Include(listing => listing.ApartmentDetails)
            .Include(listing => listing.HouseDetails)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        await LoadSelectedListingCollectionsAsync(
            listings,
            cancellationToken);

        return new PagedResult<Listing>(
            listings,
            page,
            pageSize,
            totalCount);
    }

    public async Task<ComparableListingsReadResult>
    GetComparableListingsReadOnlyAsync(
        Guid sourceListingId,
        string languageCode,
        int limit,
        CancellationToken cancellationToken)
    {
        string requestedLanguagePattern =
            EscapeLikePattern(languageCode);

        string macedonianLanguagePattern =
            EscapeLikePattern("mk");

        var source = await _dbContext.Listings
            .AsNoTracking()
            .Where(listing =>
                listing.Id == sourceListingId &&
                listing.Status == ListingStatus.Active)
            .SelectMany(
                listing => listing.Translations
                    .OrderBy(translation =>
                        EF.Functions.ILike(
                            translation.LanguageCode,
                            requestedLanguagePattern,
                            LikeEscapeCharacter)
                            ? 0
                            : EF.Functions.ILike(
                                translation.LanguageCode,
                                macedonianLanguagePattern,
                                LikeEscapeCharacter)
                                ? 1
                                : 2)
                    .ThenBy(translation =>
                        EF.Functions.Collate(
                            translation.LanguageCode,
                            PostgreSqlBytewiseCollation))
                    .ThenBy(translation => translation.Id)
                    .Take(1)
                    .DefaultIfEmpty(),
                (listing, translation) => new
                {
                    listing.Id,
                    listing.ListingType,
                    listing.PropertyType,
                    listing.Currency,
                    listing.Price,
                    listing.AreaSquareMeters,

                    LanguageCode = translation == null
                        ? null
                        : translation.LanguageCode,

                    City = translation == null
                        ? null
                        : translation.City,

                    Municipality = translation == null
                        ? null
                        : translation.Municipality,

                    Neighborhood = translation == null
                        ? null
                        : translation.Neighborhood
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return new ComparableListingsReadResult(
                false,
                Array.Empty<Listing>());
        }

        if (source.Price <= 0 ||
            source.AreaSquareMeters <= 0 ||
            source.LanguageCode is null ||
            string.IsNullOrWhiteSpace(source.City))
        {
            return new ComparableListingsReadResult(
                true,
                Array.Empty<Listing>());
        }

        string cityPattern =
            EscapeLikePattern(source.City);

        bool sourceHasMunicipality =
            !string.IsNullOrWhiteSpace(source.Municipality);

        bool sourceHasNeighborhood =
            !string.IsNullOrWhiteSpace(source.Neighborhood);

        string municipalityPattern =
            sourceHasMunicipality
                ? EscapeLikePattern(source.Municipality!)
                : string.Empty;

        string neighborhoodPattern =
            sourceHasNeighborhood
                ? EscapeLikePattern(source.Neighborhood!)
                : string.Empty;

        decimal sourcePricePerSquareMeter =
            source.Price / source.AreaSquareMeters;

        IQueryable<Listing> eligibleCandidates = _dbContext.Listings
            .AsNoTracking()
            .Where(candidate =>
                candidate.Status == ListingStatus.Active &&
                candidate.Id != source.Id &&
                candidate.ListingType == source.ListingType &&
                candidate.PropertyType == source.PropertyType &&
                candidate.Currency == source.Currency &&
                candidate.Price > 0 &&
                candidate.AreaSquareMeters > 0);

        IQueryable<Guid> eligibleCandidateIds = eligibleCandidates
            .Select(candidate => candidate.Id);

        var candidateTranslations = _dbContext
            .Set<ListingTranslation>()
            .AsNoTracking()
            .Where(translation =>
                eligibleCandidateIds.Contains(translation.ListingId))
            .Select(translation => new
            {
                translation.Id,
                translation.ListingId,
                translation.LanguageCode,
                translation.City,
                translation.Municipality,
                translation.Neighborhood,
                LanguageSelectionKey =
                    (EF.Functions.ILike(
                        translation.LanguageCode,
                        requestedLanguagePattern,
                        LikeEscapeCharacter)
                        ? "0"
                        : EF.Functions.ILike(
                            translation.LanguageCode,
                            macedonianLanguagePattern,
                            LikeEscapeCharacter)
                            ? "1"
                            : "2") +
                    translation.LanguageCode
            });

        var bestLanguageSelectionKeys = candidateTranslations
            .GroupBy(translation => translation.ListingId)
            .Select(translations => new
            {
                ListingId = translations.Key,
                LanguageSelectionKey = translations.Min(translation =>
                    EF.Functions.Collate(
                        translation.LanguageSelectionKey,
                        PostgreSqlBytewiseCollation))
            });

        var effectiveCandidateTranslations =
            from translation in candidateTranslations
            join bestLanguageSelectionKey in bestLanguageSelectionKeys
                on new
                {
                    translation.ListingId,
                    LanguageSelectionKey = EF.Functions.Collate(
                        translation.LanguageSelectionKey,
                        PostgreSqlBytewiseCollation)
                }
                equals new
                {
                    bestLanguageSelectionKey.ListingId,
                    bestLanguageSelectionKey.LanguageSelectionKey
                }
            select translation;

        var candidateRows =
            from candidate in eligibleCandidates
            join translation in effectiveCandidateTranslations
                on candidate.Id equals translation.ListingId
            where translation.LanguageCode ==
                    source.LanguageCode &&

                translation.City != null &&
                translation.City.Trim() != string.Empty &&

                EF.Functions.ILike(
                    translation.City,
                    cityPattern,
                    LikeEscapeCharacter)
            select new
            {
                Listing = candidate,
                Translation = translation
            };

        var orderedCandidates = candidateRows
            .OrderBy(row =>
                sourceHasMunicipality &&
                sourceHasNeighborhood &&

                row.Translation.Municipality != null &&
                row.Translation.Municipality.Trim() !=
                    string.Empty &&

                EF.Functions.ILike(
                    row.Translation.Municipality,
                    municipalityPattern,
                    LikeEscapeCharacter) &&

                row.Translation.Neighborhood != null &&
                row.Translation.Neighborhood.Trim() !=
                    string.Empty &&

                EF.Functions.ILike(
                    row.Translation.Neighborhood,
                    neighborhoodPattern,
                    LikeEscapeCharacter)
                    ? 0

                    : sourceHasMunicipality &&

                      row.Translation.Municipality != null &&
                      row.Translation.Municipality.Trim() !=
                          string.Empty &&

                      EF.Functions.ILike(
                          row.Translation.Municipality,
                          municipalityPattern,
                          LikeEscapeCharacter)
                        ? 1
                        : 2)
            .ThenBy(row =>
                Math.Abs(
                    row.Listing.AreaSquareMeters -
                    source.AreaSquareMeters) /
                source.AreaSquareMeters)
            .ThenBy(row =>
                Math.Abs(
                    row.Listing.Price /
                    row.Listing.AreaSquareMeters -
                    sourcePricePerSquareMeter) /
                sourcePricePerSquareMeter)
            .ThenBy(row =>
                Math.Abs(
                    row.Listing.Price -
                    source.Price) /
                source.Price)
            .ThenByDescending(row =>
                row.Listing.CreatedAtUtc)
            .ThenByDescending(row =>
                row.Listing.Id);

        IQueryable<Listing> limitedListingsQuery =
            orderedCandidates
                .Select(row => row.Listing)
                .Take(limit);

        List<Listing> listings = await limitedListingsQuery
            .Include(listing => listing.ApartmentDetails)
            .Include(listing => listing.HouseDetails)
            .ToListAsync(cancellationToken);

        await LoadSelectedListingCollectionsAsync(
            listings,
            cancellationToken);

        return new ComparableListingsReadResult(
            true,
            listings);
    }

    public async Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken)
    {
        return await ApplyListingIncludes(_dbContext.Listings.AsNoTracking())
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Listing>> GetByCreatedByUserIdAsync(
        Guid createdByUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        IQueryable<Listing> query = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.CreatedByUserId == createdByUserId);

        int totalCount = await query.CountAsync(cancellationToken);

        List<Listing> listings = await ApplyListingIncludes(query)
            .OrderByDescending(listing => listing.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Listing>(
            listings,
            page,
            pageSize,
            totalCount);
    }

    public async Task<PagedResult<Listing>> GetByAgencyIdForDashboardReadOnlyAsync(
    Guid agencyId,
    ListingStatus? status,
    int page,
    int pageSize,
    CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        IQueryable<Listing> query = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.AgencyId == agencyId);

        if (status.HasValue)
        {
            query = query.Where(listing => listing.Status == status.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<Listing> listings = await ApplyListingIncludes(query)
            .OrderByDescending(listing => listing.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Listing>(
            listings,
            page,
            pageSize,
            totalCount);
    }

    public async Task<ListingImageUploadProbeReadModel?>
    GetListingImageUploadProbeReadOnlyAsync(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .AsNoTracking()
            .Where(listing =>
                listing.Id == listingId)
            .Select(listing =>
                new ListingImageUploadProbeReadModel(
                    listing.Id,
                    listing.CreatedByUserId,
                    listing.Images.Count))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IListingImageWriteScope?>
    BeginListingImageWriteAsync(
        Guid listingId,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        try
        {
            DbConnection connection =
                _dbContext.Database.GetDbConnection();

            await using DbCommand command =
                connection.CreateCommand();

            command.Transaction =
                transaction.GetDbTransaction();

            command.CommandText =
                """
            SELECT "Id"
            FROM "Listings"
            WHERE "Id" = @listingId
            FOR UPDATE;
            """;

            DbParameter listingIdParameter =
                command.CreateParameter();

            listingIdParameter.ParameterName =
                "@listingId";

            listingIdParameter.DbType =
                DbType.Guid;

            listingIdParameter.Value =
                listingId;

            command.Parameters.Add(
                listingIdParameter);

            object? lockedListingId =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (lockedListingId is null ||
                lockedListingId is DBNull)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                await transaction.DisposeAsync();

                return null;
            }

            Listing? listing =
                await _dbContext.Listings
                    .Include(currentListing =>
                        currentListing.Images)
                    .SingleOrDefaultAsync(
                        currentListing =>
                            currentListing.Id == listingId,
                        cancellationToken);

            if (listing is null)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                await transaction.DisposeAsync();

                return null;
            }

            return new ListingImageWriteScope(
                listing,
                transaction);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }
            finally
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    public async Task<Listing?> GetByIdWithImagesForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .Include(listing => listing.Images)
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public void AddListingImage(ListingImage image)
    {
        _dbContext.Set<ListingImage>().Add(image);
    }

    public void RemoveListingImage(ListingImage image)
    {
        _dbContext.Set<ListingImage>().Remove(image);
    }

    public async Task<Listing?> GetByIdForUpdateAsync(
    Guid id,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Listing> ApplyBasicFilters(
        IQueryable<Listing> query,
        GetListingsQuery filters)
    {
        if (filters.AgencyId.HasValue)
        {
            query = query.Where(listing =>
                listing.AgencyId == filters.AgencyId.Value);
        }

        if (filters.ListingType.HasValue)
        {
            query = query.Where(listing =>
                listing.ListingType == filters.ListingType.Value);
        }

        if (filters.PropertyType.HasValue)
        {
            query = query.Where(listing =>
                listing.PropertyType == filters.PropertyType.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Currency))
        {
            query = query.Where(listing =>
                listing.Currency == filters.Currency);
        }

        if (filters.MinPrice.HasValue)
        {
            query = query.Where(listing =>
                listing.Price >= filters.MinPrice.Value);
        }

        if (filters.MaxPrice.HasValue)
        {
            query = query.Where(listing =>
                listing.Price <= filters.MaxPrice.Value);
        }

        if (filters.MinAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.AreaSquareMeters >=
                filters.MinAreaSquareMeters.Value);
        }

        if (filters.MaxAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.AreaSquareMeters <=
                filters.MaxAreaSquareMeters.Value);
        }

        if (filters.MinRooms.HasValue)
        {
            query = query.Where(listing =>
                listing.Rooms.HasValue &&
                listing.Rooms.Value >= filters.MinRooms.Value);
        }

        if (filters.MaxRooms.HasValue)
        {
            query = query.Where(listing =>
                listing.Rooms.HasValue &&
                listing.Rooms.Value <= filters.MaxRooms.Value);
        }

        if (filters.HeatingType.HasValue)
        {
            query = query.Where(listing =>
                listing.HeatingType == filters.HeatingType.Value);
        }

        if (filters.FurnishingStatus.HasValue)
        {
            query = query.Where(listing =>
                listing.FurnishingStatus == filters.FurnishingStatus.Value);
        }

        if (filters.Condition.HasValue)
        {
            query = query.Where(listing =>
                listing.Condition == filters.Condition.Value);
        }

        if (filters.HasBasement.HasValue)
        {
            query = query.Where(listing =>
                listing.HasBasement == filters.HasBasement.Value);
        }

        return query;
    }

    private static IQueryable<Listing> ApplyPropertyDetailFilters(
        IQueryable<Listing> query,
        GetListingsQuery filters)
    {
        if (filters.HasElevator.HasValue)
        {
            query = query.Where(listing =>
                listing.ApartmentDetails != null &&
                listing.ApartmentDetails.HasElevator == filters.HasElevator.Value);
        }

        if (filters.ApartmentType.HasValue)
        {
            query = query.Where(listing =>
                listing.ApartmentDetails != null &&
                listing.ApartmentDetails.ApartmentType == filters.ApartmentType.Value);
        }

        if (filters.HouseType.HasValue)
        {
            query = query.Where(listing =>
                listing.HouseDetails != null &&
                listing.HouseDetails.HouseType == filters.HouseType.Value);
        }

        if (filters.MinYardAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.HouseDetails != null &&
                listing.HouseDetails.YardAreaSquareMeters >= filters.MinYardAreaSquareMeters.Value);
        }

        if (filters.MaxYardAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.HouseDetails != null &&
                listing.HouseDetails.YardAreaSquareMeters <= filters.MaxYardAreaSquareMeters.Value);
        }

        return query;
    }

    private IQueryable<Listing> ApplyEffectiveTranslationFilters(
        IQueryable<Listing> query,
        GetListingsQuery filters)
    {
        bool hasSearchText =
            filters.SearchText is not null;

        bool hasCity =
            filters.City is not null;

        bool hasMunicipality =
            filters.Municipality is not null;

        bool hasNeighborhood =
            filters.Neighborhood is not null;

        if (!hasSearchText &&
            !hasCity &&
            !hasMunicipality &&
            !hasNeighborhood)
        {
            return query;
        }

        string requestedLanguagePattern =
            EscapeLikePattern(filters.LanguageCode);

        string macedonianLanguagePattern =
            EscapeLikePattern("mk");

        string searchTextPattern = hasSearchText
            ? $"%{EscapeLikePattern(filters.SearchText!)}%"
            : string.Empty;

        string cityPattern = hasCity
            ? EscapeLikePattern(filters.City!)
            : string.Empty;

        string municipalityPattern = hasMunicipality
            ? EscapeLikePattern(filters.Municipality!)
            : string.Empty;

        string neighborhoodPattern = hasNeighborhood
            ? EscapeLikePattern(filters.Neighborhood!)
            : string.Empty;

        IQueryable<Guid> candidateListingIds = query
            .Select(listing => listing.Id);

        var candidateTranslations = _dbContext
            .Set<ListingTranslation>()
            .AsNoTracking()
            .Where(translation =>
                candidateListingIds.Contains(translation.ListingId))
            .Select(translation => new
            {
                translation.Id,
                translation.ListingId,
                translation.LanguageCode,
                translation.Title,
                translation.City,
                translation.Municipality,
                translation.Neighborhood,
                LanguageSelectionKey =
                    (EF.Functions.ILike(
                        translation.LanguageCode,
                        requestedLanguagePattern,
                        LikeEscapeCharacter)
                        ? "0"
                        : EF.Functions.ILike(
                            translation.LanguageCode,
                            macedonianLanguagePattern,
                            LikeEscapeCharacter)
                            ? "1"
                            : "2") +
                    translation.LanguageCode
            });

        var bestLanguageSelectionKeys = candidateTranslations
            .GroupBy(translation => translation.ListingId)
            .Select(translations => new
            {
                ListingId = translations.Key,
                // The unique (ListingId, LanguageCode) key means this
                // priority-plus-bytewise-language minimum identifies one row.
                // A translation-UUID tie cannot exist for that selected code.
                LanguageSelectionKey = translations.Min(translation =>
                    EF.Functions.Collate(
                        translation.LanguageSelectionKey,
                        PostgreSqlBytewiseCollation))
            });

        var effectiveTranslations =
            from translation in candidateTranslations
            join bestLanguageSelectionKey in bestLanguageSelectionKeys
                on new
                {
                    translation.ListingId,
                    LanguageSelectionKey = EF.Functions.Collate(
                        translation.LanguageSelectionKey,
                        PostgreSqlBytewiseCollation)
                }
                equals new
                {
                    bestLanguageSelectionKey.ListingId,
                    bestLanguageSelectionKey.LanguageSelectionKey
                }
            select translation;

        IQueryable<Guid> matchingListingIds = effectiveTranslations
            .Where(translation =>
                (!hasCity ||
                    (translation.City != null &&
                     EF.Functions.ILike(
                         translation.City,
                         cityPattern,
                         LikeEscapeCharacter))) &&

                (!hasMunicipality ||
                    (translation.Municipality != null &&
                     EF.Functions.ILike(
                         translation.Municipality,
                         municipalityPattern,
                         LikeEscapeCharacter))) &&

                (!hasNeighborhood ||
                    (translation.Neighborhood != null &&
                     EF.Functions.ILike(
                         translation.Neighborhood,
                         neighborhoodPattern,
                         LikeEscapeCharacter))) &&

                (!hasSearchText ||
                    EF.Functions.ILike(
                        translation.Title,
                        searchTextPattern,
                        LikeEscapeCharacter) ||

                    (translation.City != null &&
                     EF.Functions.ILike(
                         translation.City,
                         searchTextPattern,
                         LikeEscapeCharacter)) ||

                    (translation.Municipality != null &&
                     EF.Functions.ILike(
                         translation.Municipality,
                         searchTextPattern,
                         LikeEscapeCharacter)) ||

                    (translation.Neighborhood != null &&
                     EF.Functions.ILike(
                         translation.Neighborhood,
                         searchTextPattern,
                         LikeEscapeCharacter))))
            .Select(translation => translation.ListingId);

        return query.Join(
            matchingListingIds,
            listing => listing.Id,
            listingId => listingId,
            (listing, _) => listing);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "%",
                "\\%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                "\\_",
                StringComparison.Ordinal);
    }

    private static IOrderedQueryable<Listing> ApplyOrdering(
        IQueryable<Listing> query,
        ListingSortOption sortOption)
    {
        return sortOption switch
        {
            ListingSortOption.Newest =>
                query
                    .OrderByDescending(listing => listing.CreatedAtUtc)
                    .ThenByDescending(listing => listing.Id),

            ListingSortOption.PriceAsc =>
                query
                    .OrderBy(listing => listing.Price)
                    .ThenByDescending(listing => listing.CreatedAtUtc)
                    .ThenByDescending(listing => listing.Id),

            ListingSortOption.PriceDesc =>
                query
                    .OrderByDescending(listing => listing.Price)
                    .ThenByDescending(listing => listing.CreatedAtUtc)
                    .ThenByDescending(listing => listing.Id),

            _ => throw new ArgumentOutOfRangeException(
                nameof(sortOption),
                sortOption,
                "Unsupported listing sort option.")
        };
    }

    private static IQueryable<Listing> ApplyListingIncludes(IQueryable<Listing> query)
    {
        return query
            .Include(listing => listing.Translations)
            .Include(listing => listing.Images)
            .Include(listing => listing.ApartmentDetails)
            .Include(listing => listing.HouseDetails)
            .AsSplitQuery();
    }

    private async Task LoadSelectedListingCollectionsAsync(
        IReadOnlyList<Listing> listings,
        CancellationToken cancellationToken)
    {
        if (listings.Count == 0)
        {
            return;
        }

        Guid[] listingIds = listings
            .Select(listing => listing.Id)
            .ToArray();

        List<ListingTranslation> translations = await (
            from listing in _dbContext.Listings.AsNoTracking()
            join translation in _dbContext.Set<ListingTranslation>().AsNoTracking()
                on listing.Id equals translation.ListingId
            where listingIds.Contains(listing.Id)
            orderby translation.ListingId,
                translation.Id
            select translation)
            .ToListAsync(cancellationToken);

        List<ListingImage> images = await (
            from listing in _dbContext.Listings.AsNoTracking()
            join image in _dbContext.Set<ListingImage>().AsNoTracking()
                on listing.Id equals image.ListingId
            where listingIds.Contains(listing.Id)
            orderby image.ListingId,
                image.SortOrder,
                image.Id
            select image)
            .ToListAsync(cancellationToken);

        ILookup<Guid, ListingTranslation> translationsByListing =
            translations.ToLookup(translation => translation.ListingId);

        ILookup<Guid, ListingImage> imagesByListing =
            images.ToLookup(image => image.ListingId);

        foreach (Listing listing in listings)
        {
            listing.Translations = translationsByListing[listing.Id]
                .ToList();

            listing.Images = imagesByListing[listing.Id]
                .ToList();
        }
    }

    private static (int Page, int PageSize) NormalizePagination(
        int page,
        int pageSize)
    {
        page = Math.Max(page, 1);

        if (pageSize < 1)
        {
            pageSize = DefaultPageSize;
        }

        pageSize = Math.Min(pageSize, MaxPageSize);

        return (page, pageSize);
    }

    private sealed class ListingImageWriteScope
    : IListingImageWriteScope
    {
        private readonly IDbContextTransaction _transaction;

        private bool _committed;
        private bool _disposed;

        public ListingImageWriteScope(
            Listing listing,
            IDbContextTransaction transaction)
        {
            Listing = listing;
            _transaction = transaction;
        }

        public Listing Listing { get; }

        public async Task CommitAsync(
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(ListingImageWriteScope));
            }

            if (_committed)
            {
                return;
            }

            await _transaction.CommitAsync(
                cancellationToken);

            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (!_committed)
                {
                    await _transaction.RollbackAsync(
                        CancellationToken.None);
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }
    }
}
