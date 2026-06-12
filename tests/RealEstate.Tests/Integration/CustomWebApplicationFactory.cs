using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RealEstate.Tests.Integration;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("realestate_test_db")
        .WithUsername("realestate_user")
        .WithPassword("realestate_password")
        .Build();

    private string? _originalConnectionString;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            _postgresContainer.GetConnectionString());

        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            _originalConnectionString);

        await _postgresContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbContextOptionsDescriptor = services.SingleOrDefault(
                service => service.ServiceType == typeof(DbContextOptions<RealEstateDbContext>));

            if (dbContextOptionsDescriptor is not null)
            {
                services.Remove(dbContextOptionsDescriptor);
            }

            services.AddDbContext<RealEstateDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });
        });
    }
}