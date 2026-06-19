using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Application.Common.Storage;
using RealEstate.Infrastructure.Storage;
using RealEstate.Application.Common.Security;
using RealEstate.Application.Users.Repositories;
using RealEstate.Infrastructure.Security;

namespace RealEstate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<RealEstateDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IListingRepository, ListingRepository>();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasherService>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}