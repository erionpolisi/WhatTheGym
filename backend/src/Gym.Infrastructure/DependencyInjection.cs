using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Infrastructure.Email;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Retention;
using Gym.Infrastructure.Seeding;
using Gym.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IGymChainRepository, GymChainRepository>();
        services.AddScoped<IAmenityRepository, AmenityRepository>();
        services.AddScoped<IGymRepository, GymRepository>();
        services.AddScoped<IGymRatingSummaryStore, GymRatingSummaryStore>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ILegalCaseRepository, LegalCaseRepository>();
        services.AddScoped<ILegalHoldRepository, LegalHoldRepository>();
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<IContactRequestRepository, ContactRequestRepository>();
        services.AddScoped<IAnalyticsEventStore, AnalyticsEventStore>();
        services.AddScoped<IPersonalDataQuery, PersonalDataQuery>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<IGymSearchQuery, GymSearchQuery>();
        services.AddScoped<ISearchIndex, PostgresSearchIndex>();
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();
        services.AddSingleton<ISessionBucketHasher, SessionBucketHasher>();

        // Resend is used when an API key is configured; otherwise mails are logged (local development).
        var resendKey = configuration[$"{MailOptions.SectionName}:ResendApiKey"];
        if (!string.IsNullOrWhiteSpace(resendKey))
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<RetentionSweeper>();

        return services;
    }
}
