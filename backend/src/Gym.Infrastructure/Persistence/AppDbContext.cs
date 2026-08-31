using System.Text.Json;
using Gym.Application.Abstractions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gym.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<GymChain> GymChains => Set<GymChain>();

    public DbSet<Amenity> Amenities => Set<Amenity>();

    public DbSet<GymEntry> Gyms => Set<GymEntry>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<ReviewRevision> ReviewRevisions => Set<ReviewRevision>();

    public DbSet<GymRatingSummary> GymRatingSummaries => Set<GymRatingSummary>();

    public DbSet<LegalCase> LegalCases => Set<LegalCase>();

    public DbSet<LegalCaseEvent> LegalCaseEvents => Set<LegalCaseEvent>();

    public DbSet<LegalCaseAppeal> LegalCaseAppeals => Set<LegalCaseAppeal>();

    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();

    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();

    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();

    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();

    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            throw new UniqueConstraintViolationException(postgres.ConstraintName ?? "unknown", ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasSequence<long>("legal_case_seq").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<GymChain>(b =>
        {
            b.ToTable("GymChains");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.Slug).HasMaxLength(220).IsRequired();
            b.HasIndex(c => c.Slug).IsUnique();
            b.Property(c => c.Website).HasMaxLength(500);
        });

        modelBuilder.Entity<Amenity>(b =>
        {
            b.ToTable("Amenities");
            b.HasKey(a => a.Id);
            b.Property(a => a.Name).HasMaxLength(120).IsRequired();
            b.Property(a => a.Slug).HasMaxLength(140).IsRequired();
            b.HasIndex(a => a.Slug).IsUnique();
        });

        modelBuilder.Entity<GymEntry>(b =>
        {
            b.ToTable("Gyms");
            b.HasKey(g => g.Id);
            b.Property(g => g.Name).HasMaxLength(200).IsRequired();
            b.Property(g => g.Slug).HasMaxLength(220).IsRequired();
            b.HasIndex(g => g.Slug).IsUnique();
            b.Property(g => g.District).IsRequired();
            b.HasIndex(g => g.District);
            b.Property(g => g.AddressLine).HasMaxLength(300).IsRequired();
            b.Property(g => g.PostalCode).HasMaxLength(8).IsRequired();
            b.Property(g => g.City).HasMaxLength(100).IsRequired();
            b.Property(g => g.CountryCode).HasMaxLength(2).IsRequired();
            b.Property(g => g.Website).HasMaxLength(500);
            b.Property(g => g.Phone).HasMaxLength(40);
            b.Property(g => g.Description).HasMaxLength(2000);
            b.Property(g => g.Status).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(g => g.Status);
            b.Property(g => g.AmenityIds).HasColumnType("uuid[]");
            b.HasOne(g => g.Chain).WithMany().HasForeignKey(g => g.ChainId).OnDelete(DeleteBehavior.SetNull);

            b.OwnsMany(g => g.OpeningHours, hours =>
            {
                hours.ToTable("GymOpeningHours");
                hours.WithOwner().HasForeignKey(h => h.GymId);
                hours.HasKey(h => new { h.GymId, h.IsoDayOfWeek });
            });
            b.Navigation(g => g.OpeningHours).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Property(u => u.GoogleSubject).HasMaxLength(200).IsRequired();
            b.HasIndex(u => u.GoogleSubject).IsUnique();
            b.Property(u => u.Email).HasMaxLength(254).IsRequired();
            b.HasIndex(u => u.Email);
            b.Property(u => u.DisplayName).HasMaxLength(80).IsRequired();
            b.Property(u => u.Role).HasConversion<string>().HasMaxLength(24);
            b.Property(u => u.Status).HasConversion<string>().HasMaxLength(24);
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
            b.HasIndex(t => t.UserId);
            b.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Review>(b =>
        {
            b.ToTable("Reviews");
            b.HasKey(r => r.Id);
            b.Property(r => r.Text).HasMaxLength(Review.MaxTextLength);
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(24);
            b.Property(r => r.DeletionOrigin).HasConversion<string?>().HasMaxLength(24);
            b.Property(r => r.DeletionReason).HasMaxLength(500);
            b.HasIndex(r => new { r.GymId, r.Status });
            b.HasIndex(r => new { r.UserId, r.GymId });
            // Database-side guarantee for "one active review per user and gym" (the
            // application check alone is racy under concurrent requests).
            b.HasIndex(r => new { r.UserId, r.GymId }, "IX_Reviews_UserId_GymId_Active")
                .IsUnique()
                .HasFilter("\"Status\" IN ('Published', 'UnderReview')");
            b.HasOne<GymEntry>().WithMany().HasForeignKey(r => r.GymId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);

            b.OwnsOne(r => r.Ratings, ratings =>
            {
                ratings.Property(x => x.PriceValue).HasColumnName("RatingPriceValue");
                ratings.Property(x => x.ContractTerms).HasColumnName("RatingContractTerms");
                ratings.Property(x => x.Billing).HasColumnName("RatingBilling");
                ratings.Property(x => x.CancellationExperience).HasColumnName("RatingCancellationExperience");
                ratings.Property(x => x.Equipment).HasColumnName("RatingEquipment");
                ratings.Property(x => x.Cleanliness).HasColumnName("RatingCleanliness");
                ratings.Property(x => x.Staff).HasColumnName("RatingStaff");
                ratings.Property(x => x.Crowding).HasColumnName("RatingCrowding");
                ratings.Property(x => x.ChangingRoom).HasColumnName("RatingChangingRoom");
                ratings.Property(x => x.Showers).HasColumnName("RatingShowers");
                ratings.Property(x => x.Atmosphere).HasColumnName("RatingAtmosphere");
            });
            b.Navigation(r => r.Ratings).IsRequired();
        });

        modelBuilder.Entity<ReviewRevision>(b =>
        {
            b.ToTable("ReviewRevisions");
            b.HasKey(r => r.Id);
            b.Property(r => r.TextSnapshot).HasMaxLength(Review.MaxTextLength);
            b.Property(r => r.RatingsJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(r => new { r.ReviewId, r.Version }).IsUnique();
            b.HasOne<Review>().WithMany().HasForeignKey(r => r.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GymRatingSummary>(b =>
        {
            b.ToTable("GymRatingSummaries");
            b.HasKey(s => s.GymId);
            b.Property(s => s.ScoreBasis).HasConversion<string>().HasMaxLength(24);
            b.Property(s => s.CategoriesJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(s => s.TotalScore);
            b.HasOne<GymEntry>().WithOne().HasForeignKey<GymRatingSummary>(s => s.GymId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LegalCase>(b =>
        {
            b.ToTable("LegalCases");
            b.HasKey(c => c.Id);
            b.Property(c => c.CaseNumber).HasMaxLength(32).IsRequired();
            b.HasIndex(c => c.CaseNumber).IsUnique();
            b.Property(c => c.Status).HasConversion<string>().HasMaxLength(24);
            b.Property(c => c.Classification).HasConversion<string>().HasMaxLength(40);
            b.Property(c => c.Category).HasConversion<string>().HasMaxLength(40);
            b.Property(c => c.Decision).HasConversion<string?>().HasMaxLength(24);
            b.Property(c => c.ReporterName).HasMaxLength(120).IsRequired();
            b.Property(c => c.ReporterEmail).HasMaxLength(254).IsRequired();
            b.Property(c => c.Description).HasMaxLength(LegalCase.MaxDescriptionLength).IsRequired();
            b.Property(c => c.DecisionRationale).HasMaxLength(4000);
            b.Property(c => c.StatusTokenHash).HasMaxLength(128).IsRequired();
            b.Property(c => c.AppealTokenHash).HasMaxLength(128);
            b.HasIndex(c => c.Status);
            b.HasOne<Review>().WithMany().HasForeignKey(c => c.ReviewId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LegalCaseEvent>(b =>
        {
            b.ToTable("LegalCaseEvents");
            b.HasKey(e => e.Id);
            b.Property(e => e.EventType).HasConversion<string>().HasMaxLength(40);
            b.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(24);
            b.Property(e => e.DataJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(e => new { e.LegalCaseId, e.Sequence }).IsUnique();
            b.HasOne<LegalCase>().WithMany().HasForeignKey(e => e.LegalCaseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LegalCaseAppeal>(b =>
        {
            b.ToTable("LegalCaseAppeals");
            b.HasKey(a => a.Id);
            b.Property(a => a.Text).HasMaxLength(LegalCaseAppeal.MaxTextLength).IsRequired();
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(24);
            b.Property(a => a.Outcome).HasConversion<string?>().HasMaxLength(32);
            b.Property(a => a.OutcomeRationale).HasMaxLength(4000);
            b.HasIndex(a => a.LegalCaseId);
            b.HasOne<LegalCase>().WithMany().HasForeignKey(a => a.LegalCaseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LegalHold>(b =>
        {
            b.ToTable("LegalHolds");
            b.HasKey(h => h.Id);
            b.Property(h => h.Reason).HasMaxLength(500).IsRequired();
            b.HasIndex(h => h.LegalCaseId);
            b.HasIndex(h => h.ReviewId);
            b.HasIndex(h => h.UserId);
        });

        modelBuilder.Entity<LegalDocument>(b =>
        {
            b.ToTable("LegalDocuments");
            b.HasKey(d => d.Id);
            b.Property(d => d.Type).HasConversion<string>().HasMaxLength(32);
            b.Property(d => d.Title).HasMaxLength(200).IsRequired();
            b.Property(d => d.ContentMarkdown).IsRequired();
            b.HasIndex(d => new { d.Type, d.Version }).IsUnique();
        });

        modelBuilder.Entity<ContactRequest>(b =>
        {
            b.ToTable("ContactRequests");
            b.HasKey(c => c.Id);
            b.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
            b.Property(c => c.Status).HasConversion<string>().HasMaxLength(24);
            b.Property(c => c.Name).HasMaxLength(120).IsRequired();
            b.Property(c => c.Email).HasMaxLength(254).IsRequired();
            b.Property(c => c.Message).HasMaxLength(ContactRequest.MaxMessageLength).IsRequired();
            b.HasIndex(c => c.Status);
        });

        modelBuilder.Entity<AnalyticsEvent>(b =>
        {
            b.ToTable("AnalyticsEvents");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).UseIdentityAlwaysColumn();
            b.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            b.Property(e => e.Path).HasMaxLength(200);
            b.Property(e => e.SessionBucket).HasMaxLength(64).IsRequired();
            b.HasIndex(e => e.OccurredAtUtc);
            b.HasIndex(e => e.EventType);
        });

        modelBuilder.Entity<OutboxEmail>(b =>
        {
            b.ToTable("OutboxEmails");
            b.HasKey(o => o.Id);
            b.Property(o => o.ToEmail).HasMaxLength(254).IsRequired();
            b.Property(o => o.Subject).HasMaxLength(300).IsRequired();
            b.Property(o => o.BodyText).IsRequired();
            b.Property(o => o.Kind).HasMaxLength(64).IsRequired();
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(24);
            b.Property(o => o.LastError).HasMaxLength(2000);
            b.HasIndex(o => new { o.Status, o.NextAttemptAtUtc });
        });
    }

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
}
