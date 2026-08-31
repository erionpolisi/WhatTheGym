using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class ScoreCalculatorEdgeTests
{
    public static TheoryData<RatingCategory, ScoreBasis> SingleCategoryCases => new()
    {
        { RatingCategory.PriceValue, ScoreBasis.MembershipOnly },
        { RatingCategory.ContractTerms, ScoreBasis.MembershipOnly },
        { RatingCategory.Billing, ScoreBasis.MembershipOnly },
        { RatingCategory.CancellationExperience, ScoreBasis.MembershipOnly },
        { RatingCategory.Equipment, ScoreBasis.StudioOnly },
        { RatingCategory.Cleanliness, ScoreBasis.StudioOnly },
        { RatingCategory.Staff, ScoreBasis.StudioOnly },
        { RatingCategory.Crowding, ScoreBasis.StudioOnly },
        { RatingCategory.ChangingRoom, ScoreBasis.StudioOnly },
        { RatingCategory.Showers, ScoreBasis.StudioOnly },
        { RatingCategory.Atmosphere, ScoreBasis.StudioOnly },
    };

    public static TheoryData<RatingCategory, int> CategoryValues
    {
        get
        {
            var data = new TheoryData<RatingCategory, int>();
            foreach (var category in Enum.GetValues<RatingCategory>())
            {
                foreach (var value in Enumerable.Range(1, 5))
                {
                    data.Add(category, value);
                }
            }

            return data;
        }
    }

    public static TheoryData<int, int, double> BothAreaCases => new()
    {
        { 1, 1, 1.0 },
        { 1, 5, 3.0 },
        { 2, 4, 3.0 },
        { 3, 5, 4.0 },
        { 5, 1, 3.0 },
        { 5, 5, 5.0 },
        { 4, 1, 2.5 },
        { 2, 5, 3.5 },
    };

    public static TheoryData<RatingCategory> Categories
    {
        get
        {
            var data = new TheoryData<RatingCategory>();
            foreach (var category in Enum.GetValues<RatingCategory>())
            {
                data.Add(category);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SingleCategoryCases))]
    public void Single_category_rating_sets_only_its_area_and_basis(RatingCategory category, ScoreBasis expectedBasis)
    {
        var result = ScoreCalculator.Calculate([DomainTestHelpers.Rating(category, 4)]);

        result.Basis.Should().Be(expectedBasis);
        result.TotalScore.Should().Be(4);
        result.Categories.Single(c => c.Category == category).Average.Should().Be(4);
        result.Categories.Single(c => c.Category == category).RatingCount.Should().Be(1);
        if (expectedBasis == ScoreBasis.MembershipOnly)
        {
            result.MembershipScore.Should().Be(4);
            result.StudioScore.Should().BeNull();
        }
        else
        {
            result.MembershipScore.Should().BeNull();
            result.StudioScore.Should().Be(4);
        }
    }

    [Theory]
    [MemberData(nameof(CategoryValues))]
    public void Every_category_value_uses_value_as_category_area_and_total_average(RatingCategory category, int value)
    {
        var result = ScoreCalculator.Calculate([DomainTestHelpers.Rating(category, value)]);

        result.TotalScore.Should().Be(value);
        result.Categories.Single(c => c.Category == category).Average.Should().Be(value);
        result.Categories.Where(c => c.Category != category).Should().OnlyContain(c => c.Average == null && c.RatingCount == 0);
    }

    [Theory]
    [MemberData(nameof(BothAreaCases))]
    public void Both_areas_are_weighted_exactly_fifty_fifty(int membershipValue, int studioValue, double expectedTotal)
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { PriceValue = membershipValue, Equipment = studioValue },
        ]);

        result.Basis.Should().Be(ScoreBasis.Both);
        result.MembershipScore.Should().Be(membershipValue);
        result.StudioScore.Should().Be(studioValue);
        result.TotalScore.Should().Be(expectedTotal);
    }

    [Theory]
    [InlineData(ScoreBasis.None, null, null, null)]
    [InlineData(ScoreBasis.MembershipOnly, 3.5, null, 3.5)]
    [InlineData(ScoreBasis.StudioOnly, null, 2.5, 2.5)]
    [InlineData(ScoreBasis.Both, 3.5, 2.5, 3.0)]
    public void Basis_matches_available_areas(ScoreBasis expectedBasis, double? expectedMembership, double? expectedStudio, double? expectedTotal)
    {
        var ratings = expectedBasis switch
        {
            ScoreBasis.None => Array.Empty<ReviewRatings>(),
            ScoreBasis.MembershipOnly => [new ReviewRatings { PriceValue = 3, ContractTerms = 4 }],
            ScoreBasis.StudioOnly => [new ReviewRatings { Equipment = 2, Cleanliness = 3 }],
            ScoreBasis.Both => [new ReviewRatings { PriceValue = 3, ContractTerms = 4, Equipment = 2, Cleanliness = 3 }],
            _ => throw new ArgumentOutOfRangeException(nameof(expectedBasis), expectedBasis, null),
        };

        var result = ScoreCalculator.Calculate(ratings);

        result.Basis.Should().Be(expectedBasis);
        result.MembershipScore.Should().Be(expectedMembership);
        result.StudioScore.Should().Be(expectedStudio);
        result.TotalScore.Should().Be(expectedTotal);
    }

    [Theory]
    [InlineData(5, 4, 4, 4.33)]
    [InlineData(1, 1, 2, 1.33)]
    [InlineData(2, 2, 3, 2.33)]
    [InlineData(4, 4, 5, 4.33)]
    [InlineData(1, 2, 2, 1.67)]
    [InlineData(2, 3, 3, 2.67)]
    public void Repeating_category_averages_are_rounded_away_from_zero_to_two_decimals(int first, int second, int third, double expected)
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { Equipment = first },
            new ReviewRatings { Equipment = second },
            new ReviewRatings { Equipment = third },
        ]);

        result.Categories.Single(c => c.Category == RatingCategory.Equipment).Average.Should().Be(expected);
        result.StudioScore.Should().Be(expected);
        result.TotalScore.Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 4, 1, 1, 2.5)]
    [InlineData(5, 5, 1, 1, 3.0)]
    [InlineData(1, 1, 5, 5, 3.0)]
    [InlineData(2, 3, 4, 5, 3.5)]
    [InlineData(1, 2, 3, 4, 2.5)]
    public void Area_scores_use_raw_category_averages_before_total_rounding(int membershipA, int membershipB, int studioA, int studioB, double expectedTotal)
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { PriceValue = membershipA, ContractTerms = membershipB, Equipment = studioA, Cleanliness = studioB },
        ]);

        result.TotalScore.Should().Be(expectedTotal);
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void Rating_count_counts_only_non_null_values_per_category(RatingCategory category)
    {
        var result = ScoreCalculator.Calculate(
        [
            DomainTestHelpers.Rating(category, 1),
            new ReviewRatings(),
            DomainTestHelpers.Rating(category, 5),
            DomainTestHelpers.Rating(category, 3),
        ]);

        result.ReviewCount.Should().Be(4);
        result.Categories.Single(c => c.Category == category).RatingCount.Should().Be(3);
        result.Categories.Single(c => c.Category == category).Average.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void Missing_categories_are_null_and_never_zero(RatingCategory providedCategory)
    {
        var result = ScoreCalculator.Calculate([DomainTestHelpers.Rating(providedCategory, 5)]);

        result.Categories.Where(c => c.Category != providedCategory).Should().OnlyContain(c => c.Average == null);
        result.Categories.Where(c => c.Category != providedCategory).Should().OnlyContain(c => c.RatingCount == 0);
        result.Categories.Where(c => c.Category != providedCategory).Select(c => c.Average).Should().NotContain(0);
    }

    [Fact]
    public void Empty_input_keeps_all_scores_null_and_all_categories_present()
    {
        var result = ScoreCalculator.Calculate([]);

        result.ReviewCount.Should().Be(0);
        result.Basis.Should().Be(ScoreBasis.None);
        result.TotalScore.Should().BeNull();
        result.MembershipScore.Should().BeNull();
        result.StudioScore.Should().BeNull();
        result.Categories.Select(c => c.Category).Should().BeEquivalentTo(Enum.GetValues<RatingCategory>());
    }

    [Fact]
    public void Large_input_keeps_counts_and_average_stable()
    {
        var ratings = Enumerable.Range(0, 500)
            .Select(i => new ReviewRatings { Equipment = i % 2 == 0 ? 4 : 5, PriceValue = 3 })
            .ToArray();

        var result = ScoreCalculator.Calculate(ratings);

        result.ReviewCount.Should().Be(500);
        result.Categories.Single(c => c.Category == RatingCategory.Equipment).RatingCount.Should().Be(500);
        result.Categories.Single(c => c.Category == RatingCategory.PriceValue).RatingCount.Should().Be(500);
        result.StudioScore.Should().Be(4.5);
        result.MembershipScore.Should().Be(3);
        result.TotalScore.Should().Be(3.75);
    }

    [Fact]
    public void Asymmetric_data_weights_one_membership_category_against_all_studio_categories_by_area()
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings
            {
                PriceValue = 1,
                Equipment = 5,
                Cleanliness = 5,
                Staff = 5,
                Crowding = 5,
                ChangingRoom = 5,
                Showers = 5,
                Atmosphere = 5,
            },
        ]);

        result.MembershipScore.Should().Be(1);
        result.StudioScore.Should().Be(5);
        result.TotalScore.Should().Be(3);
        result.Basis.Should().Be(ScoreBasis.Both);
    }
}
