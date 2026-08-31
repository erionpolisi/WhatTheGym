using Gym.Application.Common;
using Gym.Domain.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure.Retention;

/// <summary>
/// Applies the configured retention policies once per day. Records protected by an active
/// legal hold are always skipped; holds pause deletion entirely.
/// </summary>
public sealed class RetentionSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<RetentionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retention sweep failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retention = options.Value;
        var now = DateTimeOffset.UtcNow;

        // Raw analytics events.
        var analyticsCutoff = now.AddDays(-retention.AnalyticsDays);
        var analyticsDeleted = await context.AnalyticsEvents
            .Where(e => e.OccurredAtUtc < analyticsCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // Sent or permanently failed outbox mails.
        var outboxCutoff = now.AddDays(-retention.OutboxDays);
        var outboxDeleted = await context.OutboxEmails
            .Where(o => o.Status != OutboxEmailStatus.Pending && o.CreatedAtUtc < outboxCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // Review revisions after the review left public visibility (default 3 years),
        // unless a hold protects the review or its author.
        var revisionCutoff = now.AddYears(-retention.ReviewRevisionYears);
        var revisionsDeleted = await context.ReviewRevisions
            .Where(rev => context.Reviews.Any(r =>
                r.Id == rev.ReviewId
                && r.RemovedAtUtc != null
                && r.RemovedAtUtc < revisionCutoff
                && !context.LegalHolds.Any(h =>
                    h.ReleasedAtUtc == null && (h.ReviewId == r.Id || h.UserId == r.UserId))))
            .ExecuteDeleteAsync(cancellationToken);

        // Legal cases and their audit events after the audit retention period (default 7 years),
        // unless a hold protects the case, the reported review or the review author.
        var caseCutoff = now.AddYears(-retention.CaseAuditYears);
        var expiredCaseIds = await context.LegalCases
            .Where(c => c.Status == LegalCaseStatus.Closed
                        && c.ClosedAtUtc != null
                        && c.ClosedAtUtc < caseCutoff
                        && !context.LegalHolds.Any(h =>
                            h.ReleasedAtUtc == null
                            && (h.LegalCaseId == c.Id
                                || h.ReviewId == c.ReviewId
                                || context.Reviews.Any(r => r.Id == c.ReviewId && r.UserId == h.UserId))))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var casesDeleted = 0;
        if (expiredCaseIds.Count > 0)
        {
            await context.LegalCaseEvents.Where(e => expiredCaseIds.Contains(e.LegalCaseId)).ExecuteDeleteAsync(cancellationToken);
            await context.LegalCaseAppeals.Where(a => expiredCaseIds.Contains(a.LegalCaseId)).ExecuteDeleteAsync(cancellationToken);
            casesDeleted = await context.LegalCases.Where(c => expiredCaseIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        logger.LogInformation(
            "Retention sweep done: {Analytics} analytics events, {Outbox} outbox mails, {Revisions} review revisions, {Cases} legal cases removed.",
            analyticsDeleted, outboxDeleted, revisionsDeleted, casesDeleted);
    }
}
