using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Agencies.Commands.CreateAgency;
using RealEstate.Application.Agencies.Commands.UpdateAgency;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Queries.GetAgencyById;
using RealEstate.Application.Agencies.Queries.GetAgencyBySlug;
using RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;
using RealEstate.Application.Agencies.Queries.GetAgencyListings;
using RealEstate.Application.Agencies.Queries.GetAgencyMembers;
using RealEstate.Application.Agencies.Queries.GetMyAgencies;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Listings.Commands.ArchiveListing;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Commands.DeleteListingImage;
using RealEstate.Application.Listings.Commands.PublishListing;
using RealEstate.Application.Listings.Commands.ReorderListingImages;
using RealEstate.Application.Listings.Commands.SetPrimaryListingImage;
using RealEstate.Application.Listings.Commands.UnpublishListing;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Queries.GetMyListings;
using RealEstate.Application.Users.Queries.GetCurrentUser;
using RealEstate.Application.Users.Commands.UpdateCurrentUserProfile;
using RealEstate.Application.Users.Commands.UploadCurrentUserAvatar;
using RealEstate.Application.Users.Commands.DeleteCurrentUserAvatar;
using RealEstate.Application.Agencies.Commands.AcceptAgencyInvitation;
using RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;
using RealEstate.Application.Agencies.Queries.GetAgencyInvitations;

namespace RealEstate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateListingValidator>();
        services.AddScoped<CreateListingHandler>();
        services.AddScoped<GetListingsHandler>();
        services.AddScoped<GetListingByIdHandler>();
        services.AddScoped<PublishListingHandler>();
        services.AddScoped<UnpublishListingHandler>();
        services.AddScoped<ArchiveListingHandler>();

        services.AddScoped<UploadListingImageHandler>();
        services.AddScoped<DeleteListingImageHandler>();
        services.AddScoped<SetPrimaryListingImageHandler>();
        services.AddScoped<ReorderListingImagesHandler>();

        services.AddScoped<CreateAgencyValidator>();
        services.AddScoped<CreateAgencyHandler>();

        services.AddScoped<CreateAgencyInvitationValidator>();
        services.AddScoped<CreateAgencyInvitationHandler>();
        services.AddScoped<AcceptAgencyInvitationValidator>();
        services.AddScoped<AcceptAgencyInvitationHandler>();

        services.AddScoped<GetAgencyByIdHandler>();
        services.AddScoped<GetAgencyBySlugHandler>();
        services.AddScoped<GetMyAgenciesHandler>();
        services.AddScoped<GetAgencyMembersHandler>();
        services.AddScoped<GetAgencyInvitationsHandler>();
        services.AddScoped<GetAgencyListingsHandler>();
        services.AddScoped<GetAgencyDashboardListingsHandler>();
        services.AddScoped<UpdateAgencyValidator>();
        services.AddScoped<UpdateAgencyHandler>();
        services.AddScoped<AgencyListingAccessChecker>();
        services.AddScoped<AgencyAdminAccessChecker>();


        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();

        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<UpdateCurrentUserProfileHandler>();
        services.AddScoped<UploadCurrentUserAvatarHandler>();
        services.AddScoped<DeleteCurrentUserAvatarHandler>();

        services.AddScoped<GetMyListingsHandler>();

        return services;
    }
}
