using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstate.Infrastructure;
using RealEstate.Application;
using RealEstate.Infrastructure.Storage;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RealEstate.Api.Authentication;
using RealEstate.Api.Errors;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Common.Health;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";
string[] allowedCorsOrigins = GetAllowedCorsOrigins(
    builder.Configuration);

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
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins);
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(
                RequestIdentifierMiddleware.HeaderName);
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
.AllowAnonymous()
.WithName("GetHealth");

app.MapGet(
    "/api/health/readiness",
    HandleDatabaseReadinessAsync)
.AllowAnonymous()
.WithName("GetDatabaseReadiness");

app.MapGet(
    "/api/health/database",
    HandleDatabaseReadinessAsync)
.AllowAnonymous()
.WithName("GetDatabaseHealth");

app.UseCors(FrontendCorsPolicy);

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string[] GetAllowedCorsOrigins(
    IConfiguration configuration)
{
    return configuration
        .GetSection("Cors:AllowedOrigins")
        .GetChildren()
        .Select(section => section.Value?.Trim())
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(ValidateCorsOrigin)
        .ToArray();
}

static string ValidateCorsOrigin(
    string origin)
{
    const string InvalidCorsOriginMessage =
        "CORS allowed origins configuration contains an invalid origin.";

    bool isValidUri = Uri.TryCreate(
        origin,
        UriKind.Absolute,
        out Uri? uri);

    bool isHttpScheme = isValidUri &&
        (uri!.Scheme.Equals(
             Uri.UriSchemeHttp,
             StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(
             Uri.UriSchemeHttps,
             StringComparison.OrdinalIgnoreCase));

    if (!isValidUri ||
        !isHttpScheme ||
        origin.Contains("*", StringComparison.Ordinal) ||
        !string.IsNullOrEmpty(uri!.UserInfo) ||
        !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) ||
        origin.EndsWith("/", StringComparison.Ordinal) ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
    {
        throw new InvalidOperationException(
            InvalidCorsOriginMessage);
    }

    return origin;
}

static async Task<IResult> HandleDatabaseReadinessAsync(
    HttpContext httpContext,
    IDatabaseReadinessProbe readinessProbe,
    ILoggerFactory loggerFactory)
{
    const int ReadinessTimeoutSeconds = 3;
    const string ReadinessLogCategory =
        "RealEstate.Api.Health.DatabaseReadiness";

    ILogger logger = loggerFactory.CreateLogger(
        ReadinessLogCategory);

    using var timeoutCancellationSource =
        new CancellationTokenSource(
            TimeSpan.FromSeconds(
                ReadinessTimeoutSeconds));

    using var linkedCancellationSource =
        CancellationTokenSource.CreateLinkedTokenSource(
            httpContext.RequestAborted,
            timeoutCancellationSource.Token);

    try
    {
        bool canConnect = await readinessProbe
            .CanConnectAsync(
                linkedCancellationSource.Token)
            .WaitAsync(
                linkedCancellationSource.Token);

        if (canConnect)
        {
            return CreateDatabaseReadinessResult(
                isAvailable: true);
        }

        DatabaseReadinessLog.Unavailable(
            logger,
            "False");
    }
    catch (OperationCanceledException)
        when (httpContext.RequestAborted.IsCancellationRequested)
    {
        throw;
    }
    catch (OperationCanceledException)
        when (timeoutCancellationSource.IsCancellationRequested)
    {
        DatabaseReadinessLog.Unavailable(
            logger,
            "Timeout");
    }
    catch (Exception)
    {
        DatabaseReadinessLog.Unavailable(
            logger,
            "Exception");
    }

    return CreateDatabaseReadinessResult(
        isAvailable: false);
}

static IResult CreateDatabaseReadinessResult(
    bool isAvailable)
{
    return Results.Json(
        new
        {
            status = isAvailable
                ? "ok"
                : "unavailable",
            database = "PostgreSQL"
        },
        statusCode: isAvailable
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
}

internal static partial class DatabaseReadinessLog
{
    [LoggerMessage(
        EventId = 12003,
        Level = LogLevel.Warning,
        Message =
            "Database readiness probe completed as unavailable " +
            "with reason {Reason}.")]
    public static partial void Unavailable(
        ILogger logger,
        string reason);
}
