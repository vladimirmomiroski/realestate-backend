using System.Text.Json.Serialization;
using RealEstate.Infrastructure;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Application;

var builder = WebApplication.CreateBuilder(args);

// Services

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });


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
