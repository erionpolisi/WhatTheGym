using System.Text.Json;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Reviews;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Gym.Application.Features.Users;

public sealed record UpsertGoogleUserCommand(string GoogleSubject, string Email, bool EmailVerified, string DisplayName);

public sealed class UpsertGoogleUserCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<AuthOptions> authOptions) : ICommandHandler<UpsertGoogleUserCommand, MeDto>
{
    public async Task<Result<MeDto>> Handle(UpsertGoogleUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.GoogleSubject) || string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Failure<MeDto>(Error.Validation("auth.claims", "Google subject and email are required."));
        }

        var user = await users.GetByGoogleSubjectAsync(command.GoogleSubject, cancellationToken);
        if (user is null)
        {
            user = User.CreateFromGoogle(command.GoogleSubject, command.Email, command.EmailVerified, command.DisplayName, clock.UtcNow);
            users.Add(user);
        }
        else
        {
            if (user.Status == UserStatus.Deleted)
            {
                return Result.Failure<MeDto>(Error.Forbidden("auth.deleted", "Dieses Konto wurde geloescht."));
            }

            user.RecordLogin(command.Email, command.EmailVerified, clock.UtcNow);
        }

        // First-admin bootstrap: configured verified Google email becomes Admin while no Admin exists.
        var bootstrapEmail = authOptions.Value.BootstrapAdminEmail;
        if (!string.IsNullOrWhiteSpace(bootstrapEmail)
            && user.Role != UserRole.Admin
            && command.EmailVerified
            && string.Equals(command.Email.Trim(), bootstrapEmail.Trim(), StringComparison.OrdinalIgnoreCase)
            && await users.CountAdminsAsync(cancellationToken) == 0)
        {
            user.SetRole(UserRole.Admin, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToMeDto(user);
    }

    internal static MeDto ToMeDto(User user) =>
        new(user.Id, user.Email, user.EmailVerified, user.DisplayName, user.Role.ToString());
}

public sealed record GetMeQuery(Guid UserId);

public sealed class GetMeQueryHandler(IUserRepository users) : IQueryHandler<GetMeQuery, MeDto>
{
    public async Task<Result<MeDto>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<MeDto>(Error.Unauthorized("auth.required", "Anmeldung erforderlich."));
        }

        return UpsertGoogleUserCommandHandler.ToMeDto(user);
    }
}

public sealed record UpdateMyProfileCommand(Guid UserId, string DisplayName);

public sealed class UpdateMyProfileCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<UpdateMyProfileCommand, MeDto>
{
    public async Task<Result<MeDto>> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<MeDto>(Error.Unauthorized("auth.required", "Anmeldung erforderlich."));
        }

        var result = user.UpdateProfile(command.DisplayName, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<MeDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UpsertGoogleUserCommandHandler.ToMeDto(user);
    }
}

public sealed record ExportMyDataQuery(Guid UserId);

public sealed class ExportMyDataQueryHandler(
    IUserRepository users,
    IReviewRepository reviews,
    IPersonalDataQuery personalData,
    IClock clock) : IQueryHandler<ExportMyDataQuery, PersonalDataExportDto>
{
    public async Task<Result<PersonalDataExportDto>> Handle(ExportMyDataQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<PersonalDataExportDto>(Error.Unauthorized("auth.required", "Anmeldung erforderlich."));
        }

        var ownReviews = await reviews.ListByUserAsync(user.Id, cancellationToken);
        var revisions = new List<object>();
        foreach (var review in ownReviews)
        {
            foreach (var revision in await reviews.ListRevisionsAsync(review.Id, cancellationToken))
            {
                revisions.Add(new
                {
                    revision.ReviewId,
                    revision.Version,
                    revision.TextSnapshot,
                    Ratings = JsonSerializer.Deserialize<RatingsDto>(revision.RatingsJson, SummaryJson.Options),
                    revision.CreatedAtUtc,
                });
            }
        }

        var cases = (await personalData.ListCasesByReporterEmailAsync(user.Email, cancellationToken))
            .Select(c => (object)new { c.CaseNumber, Status = c.Status.ToString(), Category = c.Category.ToString(), c.CreatedAtUtc })
            .ToList();

        var contacts = (await personalData.ListContactRequestsByEmailAsync(user.Email, cancellationToken))
            .Select(c => (object)new { Type = c.Type.ToString(), c.Message, Status = c.Status.ToString(), c.CreatedAtUtc })
            .ToList();

        return new PersonalDataExportDto(
            clock.UtcNow,
            UpsertGoogleUserCommandHandler.ToMeDto(user),
            ownReviews.Select(CreateReviewCommandHandler.ToOwnDto).ToList(),
            revisions,
            cases,
            contacts);
    }
}

public sealed record DeleteMyAccountCommand(Guid UserId);

public sealed class DeleteMyAccountCommandHandler(
    IUserRepository users,
    IReviewRepository reviews,
    IRefreshTokenRepository refreshTokens,
    ILegalHoldRepository legalHolds,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<DeleteMyAccountCommand>
{
    public async Task<Result> Handle(DeleteMyAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure(Error.Unauthorized("auth.required", "Anmeldung erforderlich."));
        }

        await refreshTokens.RevokeAllForUserAsync(user.Id, clock.UtcNow, cancellationToken);

        var affectedGyms = new HashSet<Guid>();
        foreach (var review in await reviews.ListByUserAsync(user.Id, cancellationToken))
        {
            // Reviews under an active legal hold stay archived (non-public) until the hold is released.
            if (await legalHolds.HasActiveHoldForReviewAsync(review.Id, cancellationToken))
            {
                continue;
            }

            if (review.Status is ReviewStatus.Published or ReviewStatus.UnderReview)
            {
                review.SoftDelete(ReviewDeletionOrigin.AccountDeletion, "Konto geloescht.", clock.UtcNow);
                affectedGyms.Add(review.GymId);
            }
        }

        user.Anonymize(clock.UtcNow);

        foreach (var gymId in affectedGyms)
        {
            await scoreUpdater.RecalculateAsync(gymId, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListUsersQuery(int? Page, int? PageSize);

public sealed class ListUsersQueryHandler(IUserRepository users) : IQueryHandler<ListUsersQuery, PagedResult<UserAdminDto>>
{
    public async Task<Result<PagedResult<UserAdminDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await users.ListAsync(page, pageSize, cancellationToken);
        var items = result.Items
            .Select(u => new UserAdminDto(
                u.Id, u.Email, u.EmailVerified, u.DisplayName, u.Role.ToString(), u.Status.ToString(), u.CreatedAtUtc, u.LastLoginAtUtc))
            .ToList();
        return new PagedResult<UserAdminDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}

public sealed record SetUserRoleCommand(Guid TargetUserId, string Role);

public sealed class SetUserRoleCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SetUserRoleCommand>
{
    public async Task<Result> Handle(SetUserRoleCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(command.Role, ignoreCase: true, out var role))
        {
            return Result.Failure(Error.Validation("user.role", "Ungueltige Rolle."));
        }

        var user = await users.GetByIdAsync(command.TargetUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("user.notFound", "Das Konto wurde nicht gefunden."));
        }

        if (user.Role == UserRole.Admin && role != UserRole.Admin && await users.CountAdminsAsync(cancellationToken) <= 1)
        {
            return Result.Failure(Error.Conflict("user.lastAdmin", "Der letzte Admin kann nicht herabgestuft werden."));
        }

        user.SetRole(role, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
