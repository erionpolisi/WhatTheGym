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
        foreach (var (name, website) in ViennaCatalog.Chains)
        {
            var slug = Slug.Generate(name);
            var existing = await context.GymChains.FirstOrDefaultAsync(c => c.Slug == slug, ct);
            if (existing is null)
            {
                existing = GymChain.Create(name, slug, website, SeedStamp);
                context.GymChains.Add(existing);
            }

            chainsBySlug[slug] = existing;
        }

        var amenityIds = new List<Guid>();
        foreach (var amenityName in ViennaCatalog.Amenities)
        {
            var slug = Slug.Generate(amenityName);
            var existing = await context.Amenities.FirstOrDefaultAsync(a => a.Slug == slug, ct);
            if (existing is null)
            {
                existing = Amenity.Create(amenityName, slug, SeedStamp);
                context.Amenities.Add(existing);
            }

            amenityIds.Add(existing.Id);
        }

        await context.SaveChangesAsync(ct);

        var defaultAmenities = amenityIds.Take(7).ToList();
        var created = 0;
        foreach (var seed in ViennaCatalog.Gyms)
        {
            var slug = Slug.Generate(seed.Name);
            if (await context.Gyms.AnyAsync(g => g.Slug == slug, ct))
            {
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
                GymStatus.Active,
                SeedStamp);
            if (gymResult.IsFailure)
            {
                logger.LogWarning("Seed gym {Name} skipped: {Error}", seed.Name, gymResult.Error.Message);
                continue;
            }

            gymResult.Value.SetAmenities(defaultAmenities, SeedStamp);
            context.Gyms.Add(gymResult.Value);
            created++;
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Catalogue seed done ({Created} new gyms).", created);
    }

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
        if (await context.Users.AnyAsync(u => u.GoogleSubject == "dev-demo-1", ct))
        {
            return;
        }

        var demoUsers = new[]
        {
            User.CreateFromGoogle("dev-demo-1", "demo1@example.invalid", true, "Anna Beispiel", SeedStamp),
            User.CreateFromGoogle("dev-demo-2", "demo2@example.invalid", true, "Ben Muster", SeedStamp),
            User.CreateFromGoogle("dev-demo-3", "demo3@example.invalid", true, "Clara Test", SeedStamp),
        };
        context.Users.AddRange(demoUsers);

        var gyms = await context.Gyms.OrderBy(g => g.Slug).Take(6).ToListAsync(ct);
        if (gyms.Count == 0)
        {
            await context.SaveChangesAsync(ct);
            return;
        }

        // Owned-type instances must be unique per review; use factories, never shared instances.
        var demoRatings = new Func<ReviewRatings>[]
        {
            // Both areas rated.
            () => new ReviewRatings { PriceValue = 4, ContractTerms = 3, Billing = 4, CancellationExperience = 2, Equipment = 5, Cleanliness = 4, Staff = 4, Crowding = 3, ChangingRoom = 4, Showers = 4, Atmosphere = 5 },
            // Studio only.
            () => new ReviewRatings { Equipment = 3, Cleanliness = 2, Staff = 4, Crowding = 2, Atmosphere = 3 },
            // Membership only.
            () => new ReviewRatings { PriceValue = 5, ContractTerms = 4, Billing = 5, CancellationExperience = 4 },
        };
        var demoTexts = new[]
        {
            "Gute Geraeteauswahl und faire Preise. Zu Stosszeiten wird es allerdings eng.",
            "Sauberkeit koennte besser sein, das Team ist aber sehr freundlich.",
            "Unkomplizierte Anmeldung und transparente Abrechnung.",
        };

        var reviews = new List<Review>();
        for (var gymIndex = 0; gymIndex < gyms.Count; gymIndex++)
        {
            for (var userIndex = 0; userIndex < demoUsers.Length; userIndex++)
            {
                // Not every user reviews every gym; keep a deterministic pattern.
                if ((gymIndex + userIndex) % 2 == 0)
                {
                    var variant = (gymIndex + userIndex) % demoRatings.Length;
                    var reviewResult = Review.Create(
                        gyms[gymIndex].Id,
                        demoUsers[userIndex].Id,
                        demoRatings[variant](),
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
            var caseResult = LegalCase.Create(
                demoCaseNumber,
                reviews[0].Id,
                LegalCaseCategory.Other,
                "Demo Melder",
                "melder@example.invalid",
                "Demo-Fall fuer lokale Entwicklung: Diese Meldung dient nur zu Testzwecken.",
                new string('0', 64),
                SeedStamp);
            if (caseResult.IsSuccess)
            {
                context.LegalCases.Add(caseResult.Value);
                context.LegalCaseEvents.Add(LegalCaseEvent.Create(
                    caseResult.Value.Id, 1, LegalCaseEventType.CaseCreated, LegalActorType.Reporter, null,
                    "{\"seed\":true}", SeedStamp));
            }
        }

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Demo data seed done ({Reviews} reviews).", reviews.Count);
    }
}

internal static class LegalDocumentDrafts
{
    private const string Marker = "\n\n---\n\n**ENTWURF - anwaltlich pruefen lassen**\n";

    public static readonly IReadOnlyList<(LegalDocumentType Type, string Title, string Content)> All =
    [
        (LegalDocumentType.Imprint, "Impressum",
            """
            # Impressum

            Medieninhaber und Herausgeber: WhatTheGym (Platzhalter - vor Veroeffentlichung ergaenzen)
            Sitz: Wien, Oesterreich
            Kontakt: kontakt@whatthegym.at

            Plattform zur Bewertung von Fitnessstudios in Wien.
            Offenlegung gemaess Paragraf 25 MedienG folgt vor Produktivbetrieb.
            """ + Marker),
        (LegalDocumentType.PrivacyPolicy, "Datenschutzerklaerung",
            """
            # Datenschutzerklaerung

            Wir verarbeiten personenbezogene Daten ausschliesslich gemaess DSGVO.

            - Anmeldung erfolgt ueber Google (Auth-Code-Flow); es werden keine Passwoerter gespeichert.
            - Bewertungen sind nicht anonym und werden mit dem Anzeigenamen veroeffentlicht.
            - Meldungen zu Bewertungen werden als Rechtsfall mit revisionssicherem Verlauf dokumentiert.
            - Die Reichweitenmessung ist PII-frei: keine IP-Speicherung, kein Fingerprinting.
            - Betroffenenrechte: Auskunft (Datenexport), Loeschung (Kontoloeschung mit Anonymisierung),
              Beschwerde bei der Datenschutzbehoerde.

            Details zu allen Verarbeitungen enthaelt das Verzeichnis von Verarbeitungstaetigkeiten
            (oeffentlich abrufbar ueber die API).
            """ + Marker),
        (LegalDocumentType.TermsOfUse, "Nutzungsbedingungen",
            """
            # Nutzungsbedingungen

            1. Bewertungen muessen auf eigenen Erfahrungen beruhen und wahrheitsgemaess sein.
            2. Rechtswidrige Inhalte (Beleidigung, Verleumdung, Verletzung von Rechten Dritter) sind untersagt.
            3. Gemeldete Inhalte bleiben waehrend der Pruefung grundsaetzlich online; offensichtlich
               rechtswidrige Inhalte koennen im Schnellverfahren voruebergehend ausgeblendet werden.
            4. Entscheidungen koennen mindestens sechs Monate lang angefochten werden.
            5. Konten koennen jederzeit geloescht werden; Inhalte werden entsprechend den
               Aufbewahrungsregeln anonymisiert oder entfernt.
            """ + Marker),
    ];
}
