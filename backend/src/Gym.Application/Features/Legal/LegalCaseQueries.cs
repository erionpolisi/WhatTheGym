using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Application.Features.Legal;

public sealed record GetCaseStatusByTokenQuery(string CaseNumber, string Token);

public sealed class GetCaseStatusByTokenQueryHandler(
    ILegalCaseRepository cases,
    ISecureTokenService tokens) : IQueryHandler<GetCaseStatusByTokenQuery, LegalCaseStatusPublicDto>
{
    public async Task<Result<LegalCaseStatusPublicDto>> Handle(GetCaseStatusByTokenQuery query, CancellationToken cancellationToken)
    {
        var legalCase = await cases.GetByCaseNumberAsync(query.CaseNumber, cancellationToken);
        if (legalCase is null || !string.Equals(legalCase.StatusTokenHash, tokens.Hash(query.Token), StringComparison.Ordinal))
        {
            return Result.Failure<LegalCaseStatusPublicDto>(Error.NotFound("legalCase.invalid", "Der Fall wurde nicht gefunden oder der Link ist ungueltig."));
        }

        return new LegalCaseStatusPublicDto(
            legalCase.CaseNumber,
            legalCase.Status.ToString(),
            legalCase.Decision?.ToString(),
            legalCase.CreatedAtUtc,
            legalCase.DecidedAtUtc,
            legalCase.AppealDeadlineUtc);
    }
}

public sealed record ListCasesQuery(string? Status, int? Page, int? PageSize);

public sealed class ListCasesQueryHandler(ILegalCaseRepository cases) : IQueryHandler<ListCasesQuery, PagedResult<LegalCaseListItemDto>>
{
    public async Task<Result<PagedResult<LegalCaseListItemDto>>> Handle(ListCasesQuery query, CancellationToken cancellationToken)
    {
        LegalCaseStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<LegalCaseStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                return Result.Failure<PagedResult<LegalCaseListItemDto>>(Error.Validation("legalCase.status", "Ungueltiger Status."));
            }

            status = parsed;
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await cases.ListAsync(status, page, pageSize, cancellationToken);
        var items = result.Items
            .Select(c => new LegalCaseListItemDto(
                c.Id, c.CaseNumber, c.ReviewId, c.Status.ToString(), c.Classification.ToString(),
                c.Category.ToString(), c.Decision?.ToString(), c.CreatedAtUtc, c.DecidedAtUtc))
            .ToList();
        return new PagedResult<LegalCaseListItemDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}

public sealed record GetCaseDetailQuery(Guid CaseId);

public sealed class GetCaseDetailQueryHandler(ILegalCaseRepository cases) : IQueryHandler<GetCaseDetailQuery, LegalCaseDetailDto>
{
    public async Task<Result<LegalCaseDetailDto>> Handle(GetCaseDetailQuery query, CancellationToken cancellationToken)
    {
        var legalCase = await cases.GetByIdAsync(query.CaseId, cancellationToken);
        if (legalCase is null)
        {
            return Result.Failure<LegalCaseDetailDto>(Error.NotFound("legalCase.notFound", "Der Fall wurde nicht gefunden."));
        }

        var events = await cases.ListEventsAsync(legalCase.Id, cancellationToken);
        var appeals = await cases.ListAppealsAsync(legalCase.Id, cancellationToken);

        return new LegalCaseDetailDto(
            legalCase.Id,
            legalCase.CaseNumber,
            legalCase.ReviewId,
            legalCase.Status.ToString(),
            legalCase.Classification.ToString(),
            legalCase.Category.ToString(),
            legalCase.ReporterName,
            legalCase.ReporterEmail,
            legalCase.Description,
            legalCase.Decision?.ToString(),
            legalCase.DecisionRationale,
            legalCase.CreatedAtUtc,
            legalCase.DecidedAtUtc,
            legalCase.ClosedAtUtc,
            legalCase.AppealDeadlineUtc,
            events.Select(e => new LegalCaseEventDto(
                e.Sequence, e.EventType.ToString(), e.ActorType.ToString(), e.ActorId, e.DataJson, e.CreatedAtUtc)).ToList(),
            appeals.Select(a => new LegalCaseAppealDto(
                a.Id, a.Status.ToString(), a.Outcome?.ToString(), a.Text, a.OutcomeRationale, a.CreatedAtUtc, a.DecidedAtUtc)).ToList());
    }
}

public sealed record TransparencyReportQuery(int Year);

public sealed class TransparencyReportQueryHandler(ILegalCaseRepository cases) : IQueryHandler<TransparencyReportQuery, TransparencyReportDto>
{
    public async Task<Result<TransparencyReportDto>> Handle(TransparencyReportQuery query, CancellationToken cancellationToken)
    {
        if (query.Year is < 2024 or > 2100)
        {
            return Result.Failure<TransparencyReportDto>(Error.Validation("transparency.year", "Ungueltiges Jahr."));
        }

        var counts = await cases.GetTransparencyCountsAsync(query.Year, cancellationToken);
        return new TransparencyReportDto(
            counts.Year,
            counts.TotalReports,
            counts.KeptOnline,
            counts.FullyRemoved,
            counts.PendingCases,
            counts.FastTrackCases,
            counts.AppealsSubmitted,
            counts.AppealsReversed,
            "Aggregierte Kennzahlen ohne Personenbezug. Faelle werden nach Eingangsjahr gezaehlt.");
    }
}
