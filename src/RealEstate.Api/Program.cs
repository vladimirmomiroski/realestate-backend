using RealEstate.Infrastructure;
using RealEstate.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

app.Run();

