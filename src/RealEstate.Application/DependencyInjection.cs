using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Commands.DeleteListingImage;
using RealEstate.Application.Listings.Commands.ReorderListingImages;
using RealEstate.Application.Listings.Commands.SetPrimaryListingImage;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Queries.GetMyListings;

namespace RealEstate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateListingValidator>();
        services.AddScoped<CreateListingHandler>();
        services.AddScoped<GetListingsHandler>();
        services.AddScoped<GetListingByIdHandler>();
        services.AddScoped<UploadListingImageHandler>();
        services.AddScoped<DeleteListingImageHandler>();
        services.AddScoped<SetPrimaryListingImageHandler>();
        services.AddScoped<ReorderListingImagesHandler>();

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<GetMyListingsHandler>();

        return services;
    }
}
