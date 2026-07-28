using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstate.Application.Users.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using RealEstate.Application.Common;
using RealEstate.Tests.Integration.Api;


namespace RealEstate.Tests.Integration.Listings;

    public sealed partial class ListingImagesEndpointTests
{
    [Fact]
    public async Task UploadImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        using MultipartFormDataContent content = CreateImageUploadContent();

        HttpResponseMessage response = await _httpClient.PostAsync(
            $"/api/listings/{Guid.NewGuid()}/images",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"/api/listings/{Guid.NewGuid()}/images/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetPrimaryImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.PutAsync(
            $"/api/listings/{Guid.NewGuid()}/images/{Guid.NewGuid()}/primary",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderImages_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        var request = new
        {
            imageIds = Array.Empty<Guid>()
        };

        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"/api/listings/{Guid.NewGuid()}/images/order",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadImage_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, _) = await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}/images");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task SetPrimaryImage_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/images/{imageId}/primary",
                new StringContent(string.Empty));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            var request = new
            {
                imageIds = new[] { imageId }
            };

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_WithDifferentActiveAgencyMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agencyMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agencyMember.UserId);

        Guid listingId = await CreateAgencyListingAsAsync(owner, agencyId);

        _httpClient.AuthorizeAs(agencyMember.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WithDifferentActiveAgencyMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agencyMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agencyMember.UserId);

        Guid listingId = await CreateAgencyListingAsAsync(owner, agencyId);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        _httpClient.AuthorizeAs(agencyMember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_WhenCreatorIsDisabled_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        string listingDirectory =
            GetListingImageDirectory(listingId);

        string[] filesBefore =
            GetDirectoryFiles(listingDirectory);

        filesBefore.Should().BeEmpty();

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using MultipartFormDataContent content =
                CreateImageUploadContent();

            // Act
            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"/api/listings/{listingId}/images",
                    content);

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        int imageCount =
            await dbContext.Set<ListingImage>()
                .AsNoTracking()
                .CountAsync(image =>
                    image.ListingId == listingId);

        imageCount.Should().Be(0);

        string[] filesAfter =
            GetDirectoryFiles(listingDirectory);

        filesAfter.Should().Equal(filesBefore);
    }

    [Fact]
    public async Task DeleteImage_WhenCreatorIsDisabled_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        Guid imageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        ListingImageState stateBefore =
            await ReadListingImageStateAsync(
                imageId);

        string imagePath =
            GetListingImageFilePath(
                listingId,
                stateBefore.StoredFileName);

        File.Exists(imagePath).Should().BeTrue();

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.DeleteAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{imageId}");

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        ListingImageState stateAfter =
            await ReadListingImageStateAsync(
                imageId);

        stateAfter.Should().Be(stateBefore);

        File.Exists(imagePath).Should().BeTrue();
    }

    [Fact]
    public async Task SetPrimaryImage_WhenCreatorIsDisabled_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        Guid firstImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Guid secondImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Dictionary<Guid, bool> primaryFlagsBefore =
            await ReadPrimaryFlagsAsync(
                listingId);

        primaryFlagsBefore[firstImageId]
            .Should()
            .BeTrue();

        primaryFlagsBefore[secondImageId]
            .Should()
            .BeFalse();

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{secondImageId}/primary",
                    new StringContent(string.Empty));

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        Dictionary<Guid, bool> primaryFlagsAfter =
            await ReadPrimaryFlagsAsync(
                listingId);

        primaryFlagsAfter.Should()
            .BeEquivalentTo(primaryFlagsBefore);

        primaryFlagsAfter.Count(pair => pair.Value)
            .Should()
            .Be(1);

        primaryFlagsAfter[firstImageId]
            .Should()
            .BeTrue();

        primaryFlagsAfter[secondImageId]
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task ReorderImages_WhenCreatorIsDisabled_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        Guid firstImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Guid secondImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Dictionary<Guid, int> sortOrdersBefore =
            await ReadSortOrdersAsync(
                listingId);

        var request = new
        {
            imageIds = new[]
            {
            secondImageId,
            firstImageId
        }
        };

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsJsonAsync(
                    $"/api/listings/{listingId}/images/order",
                    request);

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        Dictionary<Guid, int> sortOrdersAfter =
            await ReadSortOrdersAsync(
                listingId);

        sortOrdersAfter.Should()
            .BeEquivalentTo(sortOrdersBefore);

        sortOrdersAfter[firstImageId]
            .Should()
            .Be(sortOrdersBefore[firstImageId]);

        sortOrdersAfter[secondImageId]
            .Should()
            .Be(sortOrdersBefore[secondImageId]);
    }

    [Fact]
    public async Task UploadImage_WhenActorIsMissing_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        string listingDirectory =
            GetListingImageDirectory(listingId);

        string[] filesBefore =
            GetDirectoryFiles(listingDirectory);

        using WebApplicationFactory<Program> factory =
            CreateMissingActorFactory(owner.UserId);

        using HttpClient client =
            factory.CreateClient();

        client.AuthorizeAs(owner.AccessToken);

        using MultipartFormDataContent content =
            CreateImageUploadContent();

        // Act
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        int imageCount =
            await dbContext.Set<ListingImage>()
                .AsNoTracking()
                .CountAsync(image =>
                    image.ListingId == listingId);

        imageCount.Should().Be(0);

        string[] filesAfter =
            GetDirectoryFiles(listingDirectory);

        filesAfter.Should().Equal(filesBefore);
    }

    [Fact]
    public async Task DeleteImage_WhenActorIsMissing_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        Guid imageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        ListingImageState stateBefore =
            await ReadListingImageStateAsync(
                imageId);

        string imagePath =
            GetListingImageFilePath(
                listingId,
                stateBefore.StoredFileName);

        File.Exists(imagePath).Should().BeTrue();

        using WebApplicationFactory<Program> factory =
            CreateMissingActorFactory(owner.UserId);

        using HttpClient client =
            factory.CreateClient();

        client.AuthorizeAs(owner.AccessToken);

        // Act
        HttpResponseMessage response =
            await client.DeleteAsync(
                $"/api/listings/{listingId}" +
                $"/images/{imageId}");

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        ListingImageState stateAfter =
            await ReadListingImageStateAsync(
                imageId);

        stateAfter.Should().Be(stateBefore);

        File.Exists(imagePath).Should().BeTrue();
    }

    [Fact]
    public async Task SetPrimaryImage_WhenActorIsMissing_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        Guid firstImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Guid secondImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Dictionary<Guid, bool> primaryFlagsBefore =
            await ReadPrimaryFlagsAsync(
                listingId);

        primaryFlagsBefore[firstImageId]
            .Should()
            .BeTrue();

        primaryFlagsBefore[secondImageId]
            .Should()
            .BeFalse();

        using WebApplicationFactory<Program> factory =
            CreateMissingActorFactory(owner.UserId);

        using HttpClient client =
            factory.CreateClient();

        client.AuthorizeAs(owner.AccessToken);

        // Act
        HttpResponseMessage response =
            await client.PutAsync(
                $"/api/listings/{listingId}" +
                $"/images/{secondImageId}/primary",
                new StringContent(string.Empty));

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        Dictionary<Guid, bool> primaryFlagsAfter =
            await ReadPrimaryFlagsAsync(
                listingId);

        primaryFlagsAfter.Should()
            .BeEquivalentTo(primaryFlagsBefore);

        primaryFlagsAfter.Count(pair => pair.Value)
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task ReorderImages_WhenActorIsMissing_ReturnsForbiddenWithoutMutation()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        Guid firstImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Guid secondImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Dictionary<Guid, int> sortOrdersBefore =
            await ReadSortOrdersAsync(
                listingId);

        var request = new
        {
            imageIds = new[]
            {
            secondImageId,
            firstImageId
        }
        };

        using WebApplicationFactory<Program> factory =
            CreateMissingActorFactory(owner.UserId);

        using HttpClient client =
            factory.CreateClient();

        client.AuthorizeAs(owner.AccessToken);

        // Act
        HttpResponseMessage response =
            await client.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                request);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        Dictionary<Guid, int> sortOrdersAfter =
            await ReadSortOrdersAsync(
                listingId);

        sortOrdersAfter.Should()
            .BeEquivalentTo(sortOrdersBefore);
    }

    [Fact]
    public async Task ListingImageMutations_WhenCreatorIsActive_AllSucceed()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Active);

        // Upload proves Active upload eligibility.
        Guid firstImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        Guid secondImageId =
            await UploadImageAsAsync(
                listingId,
                owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Set primary
            using HttpResponseMessage primaryResponse =
                await _httpClient.PutAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{secondImageId}/primary",
                    new StringContent(string.Empty));

            primaryResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            // Reorder
            var reorderRequest = new
            {
                imageIds = new[]
                {
                secondImageId,
                firstImageId
            }
            };

            using HttpResponseMessage reorderResponse =
                await _httpClient.PutAsJsonAsync(
                    $"/api/listings/{listingId}/images/order",
                    reorderRequest);

            reorderResponse.StatusCode.Should()
                .Be(HttpStatusCode.OK);

            // Delete
            using HttpResponseMessage deleteResponse =
                await _httpClient.DeleteAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{firstImageId}");

            deleteResponse.StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        var dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<ListingImage> remainingImages =
            await dbContext.Set<ListingImage>()
                .AsNoTracking()
                .Where(image =>
                    image.ListingId == listingId)
                .ToListAsync();

        remainingImages.Should().ContainSingle();

        ListingImage remainingImage =
            remainingImages.Single();

        remainingImage.Id.Should()
            .Be(secondImageId);

        remainingImage.IsPrimary.Should()
            .BeTrue();

        remainingImage.SortOrder.Should()
            .Be(0);
    }

    [Fact]
    public async Task DeleteImage_WhenListingIsMissing_ReturnsNotFoundBeforeActorCheck()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers
                .RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(
            user.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.DeleteAsync(
                    $"/api/listings/{Guid.NewGuid()}" +
                    $"/images/{Guid.NewGuid()}");

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task SetPrimaryImage_WhenListingIsMissing_ReturnsNotFoundBeforeActorCheck()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers
                .RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(
            user.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync(
                    $"/api/listings/{Guid.NewGuid()}" +
                    $"/images/{Guid.NewGuid()}/primary",
                    new StringContent(string.Empty));

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WhenListingIsMissing_ReturnsNotFoundBeforeActorCheck()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers
                .RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(
            user.UserId,
            UserStatus.Disabled);

        var request = new
        {
            imageIds = new[]
            {
            Guid.NewGuid()
        }
        };

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsJsonAsync(
                    $"/api/listings/{Guid.NewGuid()}/images/order",
                    request);

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WhenImageIsMissing_ReturnsNotFoundForEligibleCreator()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.DeleteAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{Guid.NewGuid()}");

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task SetPrimaryImage_WhenImageIsMissing_ReturnsNotFoundForEligibleCreator()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{Guid.NewGuid()}/primary",
                    new StringContent(string.Empty));

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_WithInvalidFile_WhenCreatorIsDisabled_ReturnsBadRequestBeforeActorCheck()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using MultipartFormDataContent content =
                CreateImageUploadContent(
                    "notes.txt");

            // Act
            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"/api/listings/{listingId}/images",
                    content);

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WithEmptyImageIds_WhenCreatorIsDisabled_ReturnsBadRequestBeforeActorCheck()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        var request = new
        {
            imageIds = Array.Empty<Guid>()
        };

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsJsonAsync(
                    $"/api/listings/{listingId}/images/order",
                    request);

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WithMismatchedImageSet_ReturnsBadRequestForEligibleCreator()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        await UploadImageAsAsync(
            listingId,
            owner);

        var request = new
        {
            imageIds = new[]
            {
            Guid.NewGuid()
        }
        };

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsJsonAsync(
                    $"/api/listings/{listingId}/images/order",
                    request);

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WhenCreatorIsDisabledAndImageIsMissing_ReturnsForbidden()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.DeleteAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{Guid.NewGuid()}");

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task SetPrimaryImage_WhenCreatorIsDisabledAndImageIsMissing_ReturnsForbidden()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(
            owner.UserId,
            UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync(
                    $"/api/listings/{listingId}" +
                    $"/images/{Guid.NewGuid()}/primary",
                    new StringContent(string.Empty));

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WhenActorAndImageAreMissing_ReturnsForbidden()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        using WebApplicationFactory<Program> factory =
            CreateMissingActorFactory(owner.UserId);

        using HttpClient client =
            factory.CreateClient();

        client.AuthorizeAs(owner.AccessToken);

        // Act
        HttpResponseMessage response =
            await client.DeleteAsync(
                $"/api/listings/{listingId}" +
                $"/images/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetPrimaryImage_WhenActorAndImageAreMissing_ReturnsForbidden()
    {
        // Arrange
        (
            Guid listingId,
            AuthenticatedTestUser owner
        ) = await ListingTestHelpers
            .CreateListingWithOwnerAsync(_httpClient);

        using WebApplicationFactory<Program> factory =
            CreateMissingActorFactory(owner.UserId);

        using HttpClient client =
            factory.CreateClient();

        client.AuthorizeAs(owner.AccessToken);

        // Act
        HttpResponseMessage response =
            await client.PutAsync(
                $"/api/listings/{listingId}" +
                $"/images/{Guid.NewGuid()}/primary",
                new StringContent(string.Empty));

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private async Task SetUserStatusAsync(
        Guid userId,
        UserStatus status)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        int affectedRows =
            await dbContext.Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(
                        user => user.Status,
                        status));

        affectedRows.Should().Be(1);
    }

    private async Task<ListingImageState>
    ReadListingImageStateAsync(
        Guid imageId)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingImage>()
            .AsNoTracking()
            .Where(image => image.Id == imageId)
            .Select(image =>
                new ListingImageState(
                    image.Id,
                    image.ListingId,
                    image.OriginalFileName,
                    image.StoredFileName,
                    image.ContentType,
                    image.SizeBytes,
                    image.Url,
                    image.SortOrder,
                    image.IsPrimary))
            .SingleAsync();
    }

    private async Task<Dictionary<Guid, bool>>
    ReadPrimaryFlagsAsync(
        Guid listingId)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingImage>()
            .AsNoTracking()
            .Where(image =>
                image.ListingId == listingId)
            .ToDictionaryAsync(
                image => image.Id,
                image => image.IsPrimary);
    }

    private async Task<Dictionary<Guid, int>>
    ReadSortOrdersAsync(
        Guid listingId)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingImage>()
            .AsNoTracking()
            .Where(image =>
                image.ListingId == listingId)
            .ToDictionaryAsync(
                image => image.Id,
                image => image.SortOrder);
    }

    private string GetListingImageDirectory(
        Guid listingId)
    {
        LocalFileStorageOptions options =
            _factory.Services
                .GetRequiredService<
                    IOptions<LocalFileStorageOptions>>()
                .Value;

        return Path.Combine(
            options.RootPath,
            "listings",
            listingId.ToString());
    }

    private string GetListingImageFilePath(
        Guid listingId,
    string storedFileName)
    {
        return Path.Combine(
            GetListingImageDirectory(listingId),
            Path.GetFileName(storedFileName));
    }

    private static string[] GetDirectoryFiles(
        string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(
                directory,
                "*",
                SearchOption.TopDirectoryOnly)
            .OrderBy(
                path => path,
                StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record ListingImageState(
        Guid Id,
        Guid ListingId,
        string OriginalFileName,
        string StoredFileName,
        string ContentType,
        long SizeBytes,
        string Url,
        int SortOrder,
        bool IsPrimary);

    private WebApplicationFactory<Program>
    CreateMissingActorFactory(
        Guid missingUserId)
    {
        string connectionString =
            GetTestDatabaseConnectionString();

        return new MissingActorWebApplicationFactory(
            connectionString,
            missingUserId);
    }

    private sealed class MissingActorUserRepository(
    Guid missingUserId)
    : IUserRepository
    {
        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            User? result =
                id == missingUserId
                    ? null
                    : throw UnexpectedCall(
                        nameof(GetByIdReadOnlyAsync));

            return Task.FromResult(result);
        }

        public Task<bool> ExistsByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(ExistsByNormalizedEmailAsync));
        }

        public Task<User?>
            GetByNormalizedEmailAsync(
                string normalizedEmail,
                CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByNormalizedEmailAsync));
        }

        public Task<User?>
            GetByNormalizedEmailReadOnlyAsync(
                string normalizedEmail,
                CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByNormalizedEmailReadOnlyAsync));
        }

        public Task<User?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByIdForUpdateAsync));
        }

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(AddAsync));
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(SaveChangesAsync));
        }

        private static InvalidOperationException
            UnexpectedCall(
                string methodName)
        {
            return new InvalidOperationException(
                $"Unexpected IUserRepository call: {methodName}.");
        }
    }

    private sealed class MissingActorWebApplicationFactory(
    string connectionString,
    Guid missingUserId)
    : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Supply the value during the earliest host-configuration stage.
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                connectionString);

            // Also supply it through normal application configuration.
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] =
                                connectionString
                        });
                });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserRepository>();

                services.AddScoped<IUserRepository>(
                    _ => new MissingActorUserRepository(
                        missingUserId));
            });
        }
    }

    private string GetTestDatabaseConnectionString()
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return dbContext.Database
            .GetDbConnection()
            .ConnectionString;
    }
}

