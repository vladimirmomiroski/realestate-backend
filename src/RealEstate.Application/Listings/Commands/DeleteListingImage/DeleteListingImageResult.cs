namespace RealEstate.Application.Listings.Commands.DeleteListingImage;

public sealed class DeleteListingImageResult
{
    private DeleteListingImageResult(DeleteListingImageError error)
    {
        Error = error;
    }

    public DeleteListingImageError Error { get; }

    public bool Succeeded => Error == DeleteListingImageError.None;

    public static DeleteListingImageResult Success()
    {
        return new DeleteListingImageResult(DeleteListingImageError.None);
    }

    public static DeleteListingImageResult Failure(DeleteListingImageError error)
    {
        return new DeleteListingImageResult(error);
    }
}