using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Application.Features.Reviews;

public sealed record ListGymReviewsQuery(string GymSlug, int? Page, int? PageSize);

public sealed class ListGymReviewsQueryHandler(
    IGymRepository gyms,
    IReviewRepository reviews) : IQueryHandler<ListGymReviewsQuery, PagedResult<ReviewDto>>
{
    public async Task<Result<PagedResult<ReviewDto>>> Handle(ListGymReviewsQuery query, CancellationToken cancellationToken)
    {
        var gym = await gyms.GetBySlugAsync(query.GymSlug, cancellationToken);
        if (gym is null || !gym.IsPubliclyVisible)
        {
            return Result.Failure<PagedResult<ReviewDto>>(Error.NotFound("gym.notFound", "Das Studio wurde nicht gefunden."));
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await reviews.ListPublishedForGymAsync(gym.Id, page, pageSize, cancellationToken);

        var items = result.Items
            .Select(r => new ReviewDto(
                r.Review.Id,
                r.Review.GymId,
                new ReviewAuthorDto(r.AuthorName, r.AuthorVerified),
                RatingsDto.From(r.Review.Ratings),
                r.Review.Text,
                r.Review.EditCount,
                r.Review.CreatedAtUtc,
                r.Review.UpdatedAtUtc))
            .ToList();

        return new PagedResult<ReviewDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}

public sealed record ListMyReviewsQuery(Guid UserId);

public sealed class ListMyReviewsQueryHandler(IReviewRepository reviews) : IQueryHandler<ListMyReviewsQuery, IReadOnlyList<OwnReviewDto>>
{
    public async Task<Result<IReadOnlyList<OwnReviewDto>>> Handle(ListMyReviewsQuery query, CancellationToken cancellationToken)
    {
        var own = await reviews.ListByUserAsync(query.UserId, cancellationToken);
        return Result.Success<IReadOnlyList<OwnReviewDto>>(
            own.Select(CreateReviewCommandHandler.ToOwnDto).ToList());
    }
}

public sealed record ModerationQueueQuery(string Status, int? Page, int? PageSize);

public sealed class ModerationQueueQueryHandler(IReviewRepository reviews) : IQueryHandler<ModerationQueueQuery, PagedResult<ModerationReviewDto>>
{
    public async Task<Result<PagedResult<ModerationReviewDto>>> Handle(ModerationQueueQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReviewStatus>(query.Status, ignoreCase: true, out var status))
        {
            return Result.Failure<PagedResult<ModerationReviewDto>>(Error.Validation("moderation.status", "Ungueltiger Status."));
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await reviews.ListByStatusAsync(status, page, pageSize, cancellationToken);

        var items = result.Items
            .Select(r => new ModerationReviewDto(
                r.Review.Id,
                r.Review.GymId,
                r.GymName,
                r.GymSlug,
                r.Review.UserId,
                r.Review.Status.ToString(),
                RatingsDto.From(r.Review.Ratings),
                r.Review.Text,
                r.Review.DeletionOrigin?.ToString(),
                r.Review.DeletionReason,
                r.Review.CreatedAtUtc,
                r.Review.UpdatedAtUtc))
            .ToList();

        return new PagedResult<ModerationReviewDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}

public sealed record ModeratorRemoveReviewCommand(Guid ActorUserId, UserRole ActorRole, Guid ReviewId, string Reason);

public sealed class ModeratorRemoveReviewCommandHandler(
    IReviewRepository reviews,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ModeratorRemoveReviewCommand>
{
    public async Task<Result> Handle(ModeratorRemoveReviewCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result.Failure(Error.Validation("moderation.reason", "Eine Begruendung ist erforderlich."));
        }

        var review = await reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure(Error.NotFound("review.notFound", "Die Bewertung wurde nicht gefunden."));
        }

        var origin = command.ActorRole == UserRole.Admin ? ReviewDeletionOrigin.Admin : ReviewDeletionOrigin.Moderator;
        var deleteResult = review.SoftDelete(origin, command.Reason, clock.UtcNow);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RestoreReviewCommand(Guid ReviewId);

public sealed class RestoreReviewCommandHandler(
    IReviewRepository reviews,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RestoreReviewCommand>
{
    public async Task<Result> Handle(RestoreReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure(Error.NotFound("review.notFound", "Die Bewertung wurde nicht gefunden."));
        }

        var restoreResult = review.Restore(clock.UtcNow);
        if (restoreResult.IsFailure)
        {
            return restoreResult;
        }

        await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RebuildSummariesCommand;

public sealed class RebuildSummariesCommandHandler(
    IGymRepository gyms,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork) : ICommandHandler<RebuildSummariesCommand, int>
{
    public async Task<Result<int>> Handle(RebuildSummariesCommand command, CancellationToken cancellationToken)
    {
        var gymIds = await gyms.ListAllIdsAsync(cancellationToken);
        foreach (var gymId in gymIds)
        {
            await scoreUpdater.RecalculateAsync(gymId, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gymIds.Count;
    }
}
