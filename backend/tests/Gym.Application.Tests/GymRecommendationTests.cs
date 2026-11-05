using FluentAssertions;
using Gym.Application.Abstractions;
using Gym.Application.Contracts;
using Gym.Application.Features.Chains;
using Gym.Application.Features.Gyms;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Application.Tests;

internal sealed class AppFakeRecommendationCatalog : IRecommendationCatalogQuery
{
    public List<RecommendationCatalogRow> Rows { get; } = [];

    public Task<IReadOnlyList<RecommendationCatalogRow>> ListPublicGymsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RecommendationCatalogRow>>(Rows.ToList());
}

internal sealed class AppFakeChainSearchQuery : IChainSearchQuery
{
    public ChainSearchCriteria? LastCriteria { get; private set; }

    public List<ChainSearchRow> Rows { get; } = [];

    public Task<IReadOnlyList<ChainSearchRow>> SearchAsync(ChainSearchCriteria criteria, CancellationToken cancellationToken)
    {
        LastCriteria = criteria;
        return Task.FromResult<IReadOnlyList<ChainSearchRow>>(Rows.ToList());
    }
}

public sealed class RecommendGymsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RecommendationCatalogRow Row(
        string name,
        int district = 10,
        GymStatus status = GymStatus.Active,
        TrainingType trainingTypes = TrainingType.Strength | TrainingType.Cardio,
        TargetAudience audience = TargetAudience.General,
        IReadOnlyList<Guid>? amenityIds = null,
        int reviewCount = 0,
        double? totalScore = null) =>
        new(
            Guid.NewGuid(),
            name,
            name.ToLowerInvariant().Replace(' ', '-'),
            district,
            "Teststrasse 1",
            "1100",
            status,
            "Kette",
            "kette",
            trainingTypes,
            audience,
            amenityIds ?? [],
            reviewCount,
            totalScore,
            totalScore,
            totalScore,
            totalScore is null ? ScoreBasis.None : ScoreBasis.Both);

    private static RecommendGymsQueryHandler CreateSut(
        AppFakeRecommendationCatalog catalog,
        AppFakeAmenityRepository? amenities = null) =>
        new(catalog, amenities ?? new AppFakeAmenityRepository());

    [Fact]
    public async Task Male_users_never_get_women_only_gyms()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Mrs Sporty", audience: TargetAudience.WomenOnly));
        catalog.Rows.Add(Row("FitInn"));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery("male", false, null, null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.Gym.Name == "FitInn");
    }

    [Fact]
    public async Task Women_only_required_filters_to_women_only_gyms()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Mrs Sporty", audience: TargetAudience.WomenOnly));
        catalog.Rows.Add(Row("FitInn"));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery("female", true, null, null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.Gym.Name == "Mrs Sporty");
        result.Value[0].Reasons.Should().Contain(r => r.Contains("Frauen-Studio"));
    }

    [Fact]
    public async Task Male_plus_women_only_required_is_a_validation_error()
    {
        var result = await CreateSut(new AppFakeRecommendationCatalog()).Handle(
            new RecommendGymsQuery("male", true, null, null, null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("recommendation.womenOnly");
    }

    [Fact]
    public async Task District_ranking_prefers_higher_priority_districts()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Zweite Wahl", district: 15));
        catalog.Rows.Add(Row("Erste Wahl", district: 16));
        catalog.Rows.Add(Row("Anderswo", district: 1));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, [16, 15], null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Gym.Name.Should().Be("Erste Wahl");
        result.Value[0].MatchPercent.Should().BeGreaterThan(result.Value[1].MatchPercent);
        result.Value[0].Reasons.Should().Contain(r => r.Contains("Priorität 1"));
    }

    [Fact]
    public async Task Training_type_overlap_increases_match_and_adds_reason()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Nur Kraft", trainingTypes: TrainingType.Strength));
        catalog.Rows.Add(Row("Alles", trainingTypes: TrainingType.Strength | TrainingType.Cardio | TrainingType.Wellness));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, null, ["Strength", "Wellness"], null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Gym.Name.Should().Be("Alles");
        result.Value[0].Reasons.Should().Contain(r => r.Contains("Krafttraining") && r.Contains("Wellness"));
        result.Value[0].MatchPercent.Should().BeGreaterThan(result.Value[1].MatchPercent);
    }

    [Fact]
    public async Task Amenity_matches_contribute_and_are_explained()
    {
        var amenities = new AppFakeAmenityRepository();
        var sauna = Amenity.Create("Sauna", "sauna", Now);
        amenities.Add(sauna);

        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Mit Sauna", amenityIds: [sauna.Id]));
        catalog.Rows.Add(Row("Ohne Sauna"));

        var result = await CreateSut(catalog, amenities).Handle(
            new RecommendGymsQuery(null, false, null, null, ["sauna"], null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Gym.Name.Should().Be("Mit Sauna");
        result.Value[0].Reasons.Should().Contain(r => r.Contains("Sauna"));
        result.Value[0].MatchPercent.Should().BeGreaterThan(result.Value[1].MatchPercent);
    }

    [Fact]
    public async Task Missing_score_counts_as_neutral_not_zero()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Unbewertet"));
        catalog.Rows.Add(Row("Schlecht bewertet", reviewCount: 5, totalScore: 1.0));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, null, null, null, "high", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Gym.Name.Should().Be("Unbewertet");
        result.Value[0].MatchPercent.Should().Be(50);
        result.Value[0].Notes.Should().Contain(n => n.Contains("keine Community-Bewertungen"));
        result.Value[1].MatchPercent.Should().Be(0);
    }

    [Fact]
    public async Task Good_scores_beat_neutral_when_reviews_exist()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Top bewertet", reviewCount: 12, totalScore: 4.5));
        catalog.Rows.Add(Row("Unbewertet"));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, null, null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Gym.Name.Should().Be("Top bewertet");
        result.Value[0].Reasons.Should().Contain(r => r.Contains("4,5") && r.Contains("12 Bewertungen"));
    }

    [Fact]
    public async Task Temporarily_closed_gyms_are_penalized_with_note()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Zu", status: GymStatus.TemporarilyClosed, reviewCount: 3, totalScore: 5.0));
        catalog.Rows.Add(Row("Offen", reviewCount: 3, totalScore: 5.0));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, null, null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Gym.Name.Should().Be("Offen");
        result.Value[1].Notes.Should().Contain(n => n.Contains("vorübergehend geschlossen"));
    }

    [Fact]
    public async Task Women_only_gym_gets_note_when_gender_not_given()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("Mrs Sporty", audience: TargetAudience.WomenOnly));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, null, null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Notes.Should().Contain(n => n.Contains("nur für Frauen"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    public async Task Invalid_districts_are_rejected(int district)
    {
        var result = await CreateSut(new AppFakeRecommendationCatalog()).Handle(
            new RecommendGymsQuery(null, false, [district], null, null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("recommendation.districts");
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Yoga")]
    public async Task Unknown_training_types_are_rejected(string trainingType)
    {
        var result = await CreateSut(new AppFakeRecommendationCatalog()).Handle(
            new RecommendGymsQuery(null, false, null, [trainingType], null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("recommendation.trainingTypes");
    }

    [Fact]
    public async Task Invalid_gender_is_rejected()
    {
        var result = await CreateSut(new AppFakeRecommendationCatalog()).Handle(
            new RecommendGymsQuery("diverse-unknown", false, null, null, null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("recommendation.gender");
    }

    [Fact]
    public async Task Limit_defaults_to_three_and_is_clamped()
    {
        var catalog = new AppFakeRecommendationCatalog();
        for (var i = 0; i < 20; i++)
        {
            catalog.Rows.Add(Row($"Gym {i:00}"));
        }

        var sut = CreateSut(catalog);

        var byDefault = await sut.Handle(new RecommendGymsQuery(null, false, null, null, null, null, null), CancellationToken.None);
        byDefault.Value.Should().HaveCount(3);

        var clamped = await sut.Handle(new RecommendGymsQuery(null, false, null, null, null, null, 99), CancellationToken.None);
        clamped.Value.Should().HaveCount(10);
    }

    [Fact]
    public async Task Unknown_amenity_slugs_are_ignored()
    {
        var catalog = new AppFakeRecommendationCatalog();
        catalog.Rows.Add(Row("FitInn", reviewCount: 2, totalScore: 4.0));

        var result = await CreateSut(catalog).Handle(
            new RecommendGymsQuery(null, false, null, null, ["does-not-exist"], null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}

public sealed class SearchChainsHandlerTests
{
    [Fact]
    public async Task Invalid_district_is_rejected()
    {
        var search = new AppFakeChainSearchQuery();
        var sut = new SearchChainsQueryHandler(search);

        var result = await sut.Handle(new SearchChainsQuery(null, 24, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("search.district");
    }

    [Fact]
    public async Task Term_is_trimmed_and_rows_are_mapped()
    {
        var search = new AppFakeChainSearchQuery();
        var id = Guid.NewGuid();
        search.Rows.Add(new ChainSearchRow(
            id, "FitInn", "fitinn", "https://www.fitinn.at",
            TrainingType.Strength | TrainingType.Cardio, TargetAudience.General,
            17, 42, 4.256));

        var sut = new SearchChainsQueryHandler(search);
        var result = await sut.Handle(new SearchChainsQuery("  fitinn  ", 10, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        search.LastCriteria!.Term.Should().Be("fitinn");
        search.LastCriteria.District.Should().Be(10);
        search.LastCriteria.MinTotalScore.Should().Be(3);

        var item = result.Value.Single();
        item.Id.Should().Be(id);
        item.GymCount.Should().Be(17);
        item.ReviewCount.Should().Be(42);
        item.AverageScore.Should().Be(4.26);
        item.TrainingTypes.Should().BeEquivalentTo("Strength", "Cardio");
        item.TargetAudience.Should().Be("General");
    }
}
