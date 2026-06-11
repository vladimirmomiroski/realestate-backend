using System.Text.Json.Serialization;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Infrastructure;
using RealEstate.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<CreateListingValidator>();
builder.Services.AddScoped<CreateListingHandler>();
builder.Services.AddScoped<GetListingsHandler>();
builder.Services.AddScoped<GetListingByIdHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/health", () => new
{
    status = "ok",
    app = "RealEstate.Api"
})
.WithName("GetHealth");

app.MapGet("/api/health/database", async (RealEstateDbContext dbContext) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();

    return Results.Ok(new
    {
        status = canConnect ? "ok" : "unavailable",
        database = "PostgreSQL"
    });
})
.WithName("GetDatabaseHealth");

app.MapControllers();

app.Run();
