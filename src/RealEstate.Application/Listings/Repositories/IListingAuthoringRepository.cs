using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Repositories;

public interface IListingAuthoringWriteScope : IAsyncDisposable
{
    Listing Listing { get; }

    void AddTranslation(ListingTranslation translation);

    void RemoveTranslation(ListingTranslation translation);

    void MarkListingModified();

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task CommitAsync(
        CancellationToken cancellationToken);
}

public interface IListingAuthoringRepository
{
    Task<Listing?> GetByIdReadOnlyAsync(
        Guid listingId,
        CancellationToken cancellationToken);

    Task<IListingAuthoringWriteScope?> BeginWriteAsync(
        Guid listingId,
        CancellationToken cancellationToken);
}
