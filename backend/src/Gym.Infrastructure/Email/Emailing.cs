using System.Net.Http.Json;
using Gym.Application.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string bodyText, CancellationToken cancellationToken);
}

/// <summary>Sends transactional mail through the Resend API.</summary>
public sealed class ResendEmailSender(HttpClient httpClient, IOptions<MailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string bodyText, CancellationToken cancellationToken)
    {
        var mail = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mail.ResendApiKey);
        request.Content = JsonContent.Create(new
        {
            from = $"{mail.FromName} <{mail.FromAddress}>",
            to = new[] { to },
            subject,
            text = bodyText,
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend returned {(int)response.StatusCode}: {body}");
        }
    }
}

/// <summary>Local development fallback: logs mails instead of sending them.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string bodyText, CancellationToken cancellationToken)
    {
        logger.LogInformation("DEV MAIL (not sent) to {To}: {Subject}\n{Body}", to, subject, bodyText);
        return Task.CompletedTask;
    }
}

/// <summary>Processes the persistent outbox with retry and exponential backoff.</summary>
public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processing iteration failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var now = DateTimeOffset.UtcNow;

        var due = await context.OutboxEmails
            .Where(o => o.Status == OutboxEmailStatus.Pending && o.NextAttemptAtUtc <= now)
            .OrderBy(o => o.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var email in due)
        {
            try
            {
                await sender.SendAsync(email.ToEmail, email.Subject, email.BodyText, cancellationToken);
                email.MarkSent(DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Sending outbox mail {Id} failed (attempt {Attempt}).", email.Id, email.AttemptCount + 1);
                email.MarkAttemptFailed(ex.Message, DateTimeOffset.UtcNow);
            }
        }

        if (due.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
