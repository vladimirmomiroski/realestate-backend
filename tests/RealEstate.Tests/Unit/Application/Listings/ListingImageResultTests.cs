using FluentAssertions;
using RealEstate.Application.Listings.Commands.DeleteListingImage;
using RealEstate.Application.Listings.Commands.ReorderListingImages;
using RealEstate.Application.Listings.Commands.SetPrimaryListingImage;
using RealEstate.Application.Listings.Commands.UploadListingImage;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class ListingImageResultTests
{
    [Fact]
    public void FailureFactories_RejectNoneAndUnknownErrors()
    {
        Action[] actions =
        [
            () => UploadListingImageResult.Failure(UploadListingImageError.None),
            () => UploadListingImageResult.Failure((UploadListingImageError)int.MaxValue),
            () => DeleteListingImageResult.Failure(DeleteListingImageError.None),
            () => DeleteListingImageResult.Failure((DeleteListingImageError)int.MaxValue),
            () => SetPrimaryListingImageResult.Failure(SetPrimaryListingImageError.None),
            () => SetPrimaryListingImageResult.Failure((SetPrimaryListingImageError)int.MaxValue),
            () => ReorderListingImagesResult.Failure(ReorderListingImagesError.None),
            () => ReorderListingImagesResult.Failure((ReorderListingImagesError)int.MaxValue)
        ];

        actions.Should().AllSatisfy(action =>
            action.Should().Throw<ArgumentOutOfRangeException>());
    }

    [Fact]
    public void PayloadSuccessFactories_RejectMissingPayloads()
    {
        Action upload = () => UploadListingImageResult.Success(null!);
        Action setPrimary = () => SetPrimaryListingImageResult.Success(null!);
        Action reorderNull = () => ReorderListingImagesResult.Success(null!);
        Action reorderEmpty = () => ReorderListingImagesResult.Success([]);

        upload.Should().Throw<ArgumentNullException>();
        setPrimary.Should().Throw<ArgumentNullException>();
        reorderNull.Should().Throw<ArgumentNullException>();
        reorderEmpty.Should().Throw<ArgumentException>();
    }
}
