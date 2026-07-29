using System.Net;
using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task UnpublishListing_ShouldReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnpublishListing_ShouldReturnNotFound_WhenListingDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(user.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{Guid.NewGuid()}/unpublish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldUnpublishPersonalActiveListing_WhenUserIsOwner()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Draft);

            ListingStatus databaseStatus = await GetListingStatusAsync(listingId);
            databaseStatus.Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldReturnOk_WhenPersonalListingIsAlreadyDraft()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldReturnConflict_WhenPersonalListingIsArchived()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, ListingStatus.Archived);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldReturnForbidden_WhenUserIsNotPersonalListingOwner()
    {
        // Arrange
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetListingStatusAsync(listingId, ListingStatus.Active);

        AuthenticatedTestUser otherUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(otherUser.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(otherUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldUnpublishPersonalListing_WhenUserIsPendingVerification()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.PendingVerification);
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldReturnForbidden_WhenUserIsDisabled()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Disabled);
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldUnpublishAgencyListing_WhenUserIsActiveOwner()
    {
        // Arrange
        (Guid listingId, _, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();

        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldUnpublishAgencyListing_WhenUserIsActiveAgent()
    {
        // Arrange
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();

        await SetListingStatusAsync(listingId, ListingStatus.Active);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(agent.UserId, UserStatus.Active);

        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UnpublishListing_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        (Guid listingId, _, _) =
            await CreateAgencyListingWithOwnerAsync();

        await SetListingStatusAsync(listingId, ListingStatus.Active);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(nonMember.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyMemberStatus.Disabled)]
    [InlineData(AgencyMemberStatus.Pending)]
    public async Task UnpublishListing_ShouldReturnForbidden_WhenAgencyMemberIsNotActive(
        AgencyMemberStatus memberStatus)
    {
        // Arrange
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();

        await SetListingStatusAsync(listingId, ListingStatus.Active);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(agent.UserId, UserStatus.Active);

        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            memberStatus);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Disabled)]
    [InlineData(AgencyStatus.Rejected)]
    public async Task UnpublishListing_ShouldUnpublishAgencyListing_WhenAgencyIsNotActive(
        AgencyStatus agencyStatus)
    {
        // Arrange
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();

        await SetListingStatusAsync(listingId, ListingStatus.Active);
        await SetAgencyStatusAsync(agencyId, agencyStatus);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/unpublish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
