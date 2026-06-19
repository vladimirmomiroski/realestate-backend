using System.Text.Json.Serialization;
using RealEstate.Infrastructure;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Application;
using RealEstate.Infrastructure.Storage;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RealEstate.Api.Authentication;
using RealEstate.Application.Common.Authentication;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

var webRootPath = builder.Environment.WebRootPath;

if (string.IsNullOrWhiteSpace(webRootPath))
{
    webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
}

builder.Services.Configure<LocalFileStorageOptions>(options =>
{
    options.RootPath = Path.Combine(webRootPath, "uploads");
    options.PublicBasePath = "/uploads";
});

// Services

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:5173",
                "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT bearer token."
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");

string jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException("JWT secret is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

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

app.UseStaticFiles();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
