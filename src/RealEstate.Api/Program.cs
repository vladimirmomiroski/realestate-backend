using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstate.Infrastructure;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Application;
using RealEstate.Infrastructure.Storage;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RealEstate.Api.Authentication;
using RealEstate.Api.Errors;
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

builder.Services.AddSingleton<ApiFailureService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Replace(
    ServiceDescriptor.Singleton<IClientErrorFactory, ApiClientErrorFactory>());
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        ApiFailureService failureService = context.HttpContext
            .RequestServices
            .GetRequiredService<ApiFailureService>();

        ApiValidationProblemDetailsResponse problemDetails =
            failureService.CreateValidation(
                context.HttpContext,
                context.ModelState);

        return failureService.CreateResult(problemDetails);
    };
});

builder.Services.AddEndpointsApiExplorer();

var jwtSection = builder.Configuration.GetSection("Jwt");

string jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException("JWT secret is not configured.");

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT token. Do not include Bearer."
    });

    options.AddSecurityRequirement(openApiDocument => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", openApiDocument),
            new List<string>()
        }
    });
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = false;
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
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.Headers["WWW-Authenticate"] = "Bearer";

                ApiFailureService failureService = context.HttpContext
                    .RequestServices
                    .GetRequiredService<ApiFailureService>();

                await failureService.TryWriteAsync(
                    context.HttpContext,
                    failureService.Create(
                        context.HttpContext,
                        ApiFailureDescriptor.AuthenticationRequired));
            },
            OnForbidden = async context =>
            {
                ApiFailureService failureService = context.HttpContext
                    .RequestServices
                    .GetRequiredService<ApiFailureService>();

                await failureService.TryWriteAsync(
                    context.HttpContext,
                    failureService.Create(
                        context.HttpContext,
                        ApiFailureDescriptor.AuthorizationForbidden));
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

// Pipeline
app.UseMiddleware<RequestIdentifierMiddleware>();
app.UseMiddleware<ApiRequestCompletionLoggingMiddleware>();
app.UseExceptionHandler();

app.UseStatusCodePages(async statusCodeContext =>
{
    HttpContext httpContext = statusCodeContext.HttpContext;

    if (!ApiRequestPath.IsApi(httpContext.Request.Path))
    {
        return;
    }

    ApiFailureDescriptor? descriptor = httpContext.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound =>
            ApiFailureDescriptor.ResourceNotFound,
        StatusCodes.Status405MethodNotAllowed =>
            ApiFailureDescriptor.MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType =>
            ApiFailureDescriptor.MediaTypeNotSupported,
        _ => null
    };

    if (descriptor is null)
    {
        return;
    }

    ApiFailureService failureService = httpContext.RequestServices
        .GetRequiredService<ApiFailureService>();

    await failureService.TryWriteAsync(
        httpContext,
        failureService.Create(httpContext, descriptor));
});

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
