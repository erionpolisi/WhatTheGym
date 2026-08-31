using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Gym.Api.Auth;
using Gym.Api.Middleware;
using Gym.Application;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Users;
using Gym.Domain.Enums;
using Gym.Infrastructure;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Options
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.SectionName));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.Configure<AnalyticsOptions>(builder.Configuration.GetSection(AnalyticsOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<SessionService>();

// MVC + JSON
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd(
            "correlationId", context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Behind a TLS-terminating ingress (Azure Container Apps) client IP and scheme arrive via
// X-Forwarded-*. Only enable where the app is reachable exclusively through that ingress,
// because all proxies are trusted (KnownNetworks/KnownProxies cleared).
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Authentication: cookie BFF session + optional Google OIDC (code flow with PKCE).
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var googleConfigured = !string.IsNullOrWhiteSpace(authOptions.GoogleClientId)
    && !string.IsNullOrWhiteSpace(authOptions.GoogleClientSecret);

var authenticationBuilder = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "wtg.session";
        options.Cookie.HttpOnly = true;
        // Local Docker serves plain HTTP; everywhere else the Secure flag is mandatory
        // and must not depend on (possibly misconfigured) proxy forwarding.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(Math.Max(5, authOptions.SessionCookieMinutes));
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        // Revalidate every cookie session against the user store so role changes,
        // account deletion and server-side revocation take effect immediately.
        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.GetUserId();
            if (userId is null)
            {
                context.RejectPrincipal();
                return;
            }

            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(userId.Value, context.HttpContext.RequestAborted);
            if (user is null || user.Status != UserStatus.Active)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var principal = context.Principal!;
            var staleClaims = !string.Equals(principal.FindFirstValue(ClaimTypes.Role), user.Role.ToString(), StringComparison.Ordinal)
                || !string.Equals(principal.FindFirstValue(ClaimTypes.Email), user.Email, StringComparison.Ordinal)
                || !string.Equals(
                    principal.FindFirstValue("email_verified"),
                    user.EmailVerified ? "true" : "false",
                    StringComparison.Ordinal);
            if (staleClaims)
            {
                context.ReplacePrincipal(SessionService.BuildPrincipal(
                    new MeDto(user.Id, user.Email, user.EmailVerified, user.DisplayName, user.Role.ToString())));
                context.ShouldRenew = true;
            }
        };
    });

if (googleConfigured)
{
    authenticationBuilder.AddOpenIdConnect("Google", options =>
    {
        options.Authority = "https://accounts.google.com";
        options.ClientId = authOptions.GoogleClientId;
        options.ClientSecret = authOptions.GoogleClientSecret;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.CallbackPath = "/api/v1/auth/google/callback";
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.SaveTokens = false; // Provider tokens are never persisted or exposed.
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.ClaimActions.MapJsonKey("email_verified", "email_verified");
        options.Events.OnTicketReceived = async context =>
        {
            var principal = context.Principal!;
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var emailVerified = string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
            var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? email;

            var services = context.HttpContext.RequestServices;
            var upsert = services.GetRequiredService<ICommandHandler<UpsertGoogleUserCommand, MeDto>>();
            var result = await upsert.Handle(
                new UpsertGoogleUserCommand(subject, email, emailVerified, displayName),
                context.HttpContext.RequestAborted);

            if (result.IsFailure)
            {
                context.Response.Redirect("/api/v1/auth/login-failed");
                context.HandleResponse();
                return;
            }

            context.Principal = SessionService.BuildPrincipal(result.Value);
            var session = services.GetRequiredService<SessionService>();
            await session.IssueRefreshTokenAsync(context.HttpContext, result.Value.Id);
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Moderator", policy => policy.RequireRole("Moderator", "Admin"));
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// Rate limiting on public writes (in-memory, partitioned by client IP; no IP is persisted).
var publicWriteLimit = builder.Configuration.GetValue("RateLimits:PublicWritePerMinute", 10);
var authLimit = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 20);
var analyticsLimit = builder.Configuration.GetValue("RateLimits:AnalyticsPerMinute", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        // Fixed one-minute windows: tell well-behaved clients when to retry.
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };

    static RateLimitPartition<string> ByIp(HttpContext context, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            });

    options.AddPolicy("public-write", context => ByIp(context, publicWriteLimit, TimeSpan.FromMinutes(1)));
    options.AddPolicy("auth", context => ByIp(context, authLimit, TimeSpan.FromMinutes(1)));
    options.AddPolicy("analytics", context => ByIp(context, analyticsLimit, TimeSpan.FromMinutes(1)));
});

// CORS allowlist per environment (see README for local/staging/production origins).
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(corsOptions.AllowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Swagger is the admin interface for the MVP.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Nested request records share names across controllers; use full names for unique schema ids.
    options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", ".", StringComparison.Ordinal));
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WhatTheGym API",
        Version = "v1",
        Description = "REST API of the WhatTheGym review platform (Vienna). "
            + "Public catalogue/search endpoints, Google BFF authentication, reviews and scoring, "
            + "legal case handling, privacy endpoints. Swagger doubles as the admin interface.",
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CsrfHeaderMiddleware>();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatTheGym API v1");
    // Swagger doubles as the admin UI; inject the CSRF header for state-changing calls.
    options.UseRequestInterceptor("(req) => { req.headers['X-CSRF'] = '1'; return req; }");
});

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Startup migration + deterministic seeding (config-driven; demo data only in Development).
using (var scope = app.Services.CreateScope())
{
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(
            seedOptions.SeedCatalog,
            seedOptions.SeedDemoData && app.Environment.IsDevelopment(),
            CancellationToken.None);
    }
}

await app.RunAsync();

/// <summary>Readiness check: verifies the PostgreSQL connection.</summary>
public sealed class DatabaseReadyHealthCheck(AppDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthCheckContext, CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}

public partial class Program;
