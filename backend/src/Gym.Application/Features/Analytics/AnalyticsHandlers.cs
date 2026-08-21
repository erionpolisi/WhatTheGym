using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Gym.Application.Features.Analytics;

public sealed record RecordAnalyticsEventCommand(string EventType, string? Path, string SessionId);

public sealed class RecordAnalyticsEventCommandHandler(
    IAnalyticsEventStore store,
    ISessionBucketHasher hasher,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<AnalyticsOptions> options) : ICommandHandler<RecordAnalyticsEventCommand>
{
    public async Task<Result> Handle(RecordAnalyticsEventCommand command, CancellationToken cancellationToken)
    {
        if (!options.Value.AllowedEventTypes.Contains(command.EventType, StringComparer.Ordinal))
        {
            return Result.Failure(Error.Validation("analytics.eventType", "Der Ereignistyp ist nicht erlaubt."));
        }

        if (string.IsNullOrWhiteSpace(command.SessionId) || command.SessionId.Length > 128)
        {
            return Result.Failure(Error.Validation("analytics.session", "Ungueltige Session-Kennung."));
        }

        // Strip query strings and cap length so no free-form data can be smuggled in.
        string? path = null;
        if (!string.IsNullOrWhiteSpace(command.Path))
        {
            var withoutQuery = command.Path.Split('?', 2)[0].Split('#', 2)[0];
            path = withoutQuery.Length > 200 ? withoutQuery[..200] : withoutQuery;
        }

        store.Add(AnalyticsEvent.Create(command.EventType, path, hasher.Hash(command.SessionId), clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
