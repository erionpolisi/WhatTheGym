using System.Text.Json;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Seeding;

/// <summary>
/// Idempotent, deterministic development seeding: official Vienna catalogue, amenities,
/// legal document drafts and (Development only) demo users, reviews and one legal case.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext context, ILogger<DatabaseSeeder> logger)
{
    private static readonly DateTimeOffset SeedStamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync(bool seedCatalog, bool seedDemoData, CancellationToken cancellationToken)
    {
        await SeedLegalDocumentsAsync(cancellationToken);

        if (seedCatalog)
        {
            await SeedCatalogAsync(cancellationToken);
        }

        if (seedDemoData)
        {
            await SeedDemoDataAsync(cancellationToken);
        }
    }

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        var chainsBySlug = new Dictionary<string, GymChain>(StringComparer.Ordinal);
        foreach (var seed in SeedVocabulary.ChainSeeds)
        {
            var slug = Slug.Generate(seed.Name);
            var trainingTypes = ToTrainingTypeFlags(seed.TrainingTypes);
            var existing = await context.GymChains.FirstOrDefaultAsync(c => c.Slug == slug, ct);
            if (existing is null)
            {
                existing = GymChain.Create(
                    seed.Name,
                    slug,
                    seed.Website,
                    SeedStamp,
                    trainingTypes,
                    seed.TargetAudience);
                context.GymChains.Add(existing);
            }
            else if (existing.CreatedAtUtc == SeedStamp && existing.UpdatedAtUtc == SeedStamp)
            {
                existing.Update(
                    seed.Name,
                    seed.Website,
                    SeedStamp,
                    trainingTypes,
                    seed.TargetAudience);
            }

            chainsBySlug[slug] = existing;
        }

        var amenityIds = new List<Guid>();
        foreach (var amenityName in SeedVocabulary.Amenities)
        {
            var slug = Slug.Generate(amenityName);
            var existing = await context.Amenities.FirstOrDefaultAsync(a => a.Slug == slug, ct);
            if (existing is null)
            {
                existing = Amenity.Create(amenityName, slug, SeedStamp);
                context.Amenities.Add(existing);
            }
            else
            {
                existing.Rename(amenityName);
            }

            amenityIds.Add(existing.Id);
        }

        await context.SaveChangesAsync(ct);

        var defaultAmenities = amenityIds.Take(7).ToList();
        var created = 0;
        foreach (var seed in ViennaCatalog.Gyms)
        {
            var slug = Slug.Generate(seed.Name);
            var status = seed.Status
                ?? (seed.TemporarilyClosed == true ? GymStatus.TemporarilyClosed : GymStatus.Active);
            var existing = await context.Gyms.FirstOrDefaultAsync(g => g.Slug == slug, ct);
            if (existing is not null)
            {
                if (existing.CreatedAtUtc == SeedStamp && existing.UpdatedAtUtc == SeedStamp)
                {
                    Guid? seededChainId =
                        seed.ChainSlug is not null && chainsBySlug.TryGetValue(seed.ChainSlug, out var seededChain)
                            ? seededChain.Id
                            : null;
                    existing.Update(
                        seed.Name,
                        seededChainId,
                        seed.District,
                        seed.Address,
                        seed.PostalCode,
                        seededChainId is not null ? chainsBySlug[seed.ChainSlug!].Website : existing.Website,
                        existing.Phone,
                        existing.Description,
                        SeedStamp);
                    existing.ChangeStatus(status, SeedStamp);
                    existing.SetCatalogFacetOverrides(
                        seed.TrainingTypes is null ? null : ToTrainingTypeFlags(seed.TrainingTypes),
                        seed.TargetAudience,
                        SeedStamp);
                }

                continue;
            }

            Guid? chainId = seed.ChainSlug is not null && chainsBySlug.TryGetValue(seed.ChainSlug, out var chain)
                ? chain.Id
                : null;

            var gymResult = GymEntry.Create(
                seed.Name, slug, chainId, seed.District, seed.Address, seed.PostalCode,
                chainId is not null ? chainsBySlug[seed.ChainSlug!].Website : null,
                phone: null,
                description: null,
                status,
                SeedStamp);
            if (gymResult.IsFailure)
            {
                logger.LogWarning("Seed gym {Name} skipped: {Error}", seed.Name, gymResult.Error.Message);
                continue;
            }

            gymResult.Value.SetAmenities(defaultAmenities, SeedStamp);
            gymResult.Value.SetCatalogFacetOverrides(
                seed.TrainingTypes is null ? null : ToTrainingTypeFlags(seed.TrainingTypes),
                seed.TargetAudience,
                SeedStamp);
            context.Gyms.Add(gymResult.Value);
            created++;
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Catalogue seed done ({Created} new gyms).", created);
    }

    private static TrainingType ToTrainingTypeFlags(IReadOnlyList<TrainingType>? trainingTypes) =>
        trainingTypes?.Aggregate(TrainingType.None, (current, trainingType) => current | trainingType)
        ?? TrainingType.None;

    private async Task SeedLegalDocumentsAsync(CancellationToken ct)
    {
        foreach (var (type, title, content) in LegalDocumentDrafts.All)
        {
            if (await context.LegalDocuments.AnyAsync(d => d.Type == type, ct))
            {
                continue;
            }

            var document = LegalDocument.CreateDraft(type, 1, title, content, SeedStamp);
            document.Publish(SeedStamp);
            context.LegalDocuments.Add(document);
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        var demoSeed = EmbeddedSeedData.Demo;
        var demoUserSeeds = EmbeddedSeedData.Require(demoSeed.Users, "demo.users");
        if (demoUserSeeds.Count == 0)
        {
            throw new InvalidOperationException("Embedded seed data 'demo.users' must contain at least one user.");
        }

        if (await context.Users.AnyAsync(u => u.GoogleSubject == demoUserSeeds[0].GoogleSubject, ct))
        {
            return;
        }

        var demoUsers = demoUserSeeds
            .Select(user => User.CreateFromGoogle(
                user.GoogleSubject, user.Email, user.EmailVerified, user.DisplayName, SeedStamp))
            .ToArray();
        context.Users.AddRange(demoUsers);

        var gyms = await context.Gyms.OrderBy(g => g.Slug).Take(6).ToListAsync(ct);
        if (gyms.Count == 0)
        {
            await context.SaveChangesAsync(ct);
            return;
        }

        var demoRatings = EmbeddedSeedData.Require(demoSeed.Ratings, "demo.ratings");
        var demoTexts = EmbeddedSeedData.Require(demoSeed.Texts, "demo.texts");
        if (demoRatings.Count == 0 || demoRatings.Count != demoTexts.Count)
        {
            throw new InvalidOperationException(
                "Embedded seed data 'demo.ratings' and 'demo.texts' must contain the same non-zero number of entries.");
        }

        var reviews = new List<Review>();
        for (var gymIndex = 0; gymIndex < gyms.Count; gymIndex++)
        {
            for (var userIndex = 0; userIndex < demoUsers.Length; userIndex++)
            {
                // Not every user reviews every gym; keep a deterministic pattern.
                if ((gymIndex + userIndex) % 2 == 0)
                {
                    var variant = (gymIndex + userIndex) % demoRatings.Count;
                    var reviewResult = Review.Create(
                        gyms[gymIndex].Id,
                        demoUsers[userIndex].Id,
                        CloneRatings(demoRatings[variant]),
                        demoTexts[variant],
                        SeedStamp.AddDays(gymIndex + userIndex));
                    if (reviewResult.IsSuccess)
                    {
                        reviews.Add(reviewResult.Value);
                        context.Reviews.Add(reviewResult.Value);
                    }
                }
            }
        }

        await context.SaveChangesAsync(ct);

        // Materialize summaries for the seeded gyms.
        foreach (var gym in gyms)
        {
            var ratings = await context.Reviews.AsNoTracking()
                .Where(r => r.GymId == gym.Id && r.Status == ReviewStatus.Published)
                .Select(r => r.Ratings)
                .ToListAsync(ct);
            var score = ScoreCalculator.Calculate(ratings);
            var json = JsonSerializer.Serialize(score.Categories, AppDbContext.JsonOptions);
            var summary = await context.GymRatingSummaries.FirstOrDefaultAsync(s => s.GymId == gym.Id, ct);
            if (summary is null)
            {
                context.GymRatingSummaries.Add(GymRatingSummary.Create(gym.Id, score, json, SeedStamp));
            }
            else
            {
                summary.Apply(score, json, SeedStamp);
            }
        }

        // One demo legal case (Received) on the first demo review. The case number MUST be
        // drawn from the same database sequence production uses; a hardcoded number would
        // collide with the first real report (unique index on CaseNumber).
        if (reviews.Count > 0)
        {
            var next = await context.Database
                .SqlQuery<long>($"SELECT nextval('legal_case_seq') AS \"Value\"")
                .ToListAsync(ct);
            var demoCaseNumber = $"WTG-{SeedStamp.Year}-{next[0]:D6}";
            var legalCase = EmbeddedSeedData.Require(demoSeed.LegalCase, "demo.legalCase");
            var caseResult = LegalCase.Create(
                demoCaseNumber,
                reviews[0].Id,
                legalCase.Category,
                legalCase.ReporterName,
                legalCase.ReporterEmail,
                legalCase.Description,
                legalCase.ReportTokenHash,
                SeedStamp);
            if (caseResult.IsSuccess)
            {
                context.LegalCases.Add(caseResult.Value);
                context.LegalCaseEvents.Add(LegalCaseEvent.Create(
                    caseResult.Value.Id, 1, legalCase.EventType, legalCase.ActorType, null,
                    legalCase.MetadataJson, SeedStamp));
            }
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Demo data seed done ({Reviews} reviews).", reviews.Count);
    }

    private static ReviewRatings CloneRatings(ReviewRatings ratings) => new()
    {
        PriceValue = ratings.PriceValue,
        ContractTerms = ratings.ContractTerms,
        Billing = ratings.Billing,
        CancellationExperience = ratings.CancellationExperience,
        Equipment = ratings.Equipment,
        Cleanliness = ratings.Cleanliness,
        Staff = ratings.Staff,
        Crowding = ratings.Crowding,
        ChangingRoom = ratings.ChangingRoom,
        Showers = ratings.Showers,
        Atmosphere = ratings.Atmosphere,
    };
}
