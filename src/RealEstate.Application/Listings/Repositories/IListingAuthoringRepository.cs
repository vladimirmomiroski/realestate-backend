using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Repositories;

public interface IListingAuthoringWriteScope : IAsyncDisposable
{
    Listing Listing { get; }

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task CommitAsync(
        CancellationToken cancellationToken);
}

public interface IListingAuthoringRepository
{
    Task<IListingAuthoringWriteScope?> BeginWriteAsync(
        Guid listingId,
        CancellationToken cancellationToken);
}
