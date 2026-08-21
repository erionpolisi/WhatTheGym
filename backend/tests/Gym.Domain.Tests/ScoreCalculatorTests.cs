using FluentAssertions;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;
using Xunit;

namespace Gym.Domain.Tests;

public class ScoreCalculatorTests
{
    [Fact]
    public void Empty_input_yields_null_scores_and_basis_none()
    {
        var result = ScoreCalculator.Calculate([]);

        result.ReviewCount.Should().Be(0);
        result.TotalScore.Should().BeNull();
        result.MembershipScore.Should().BeNull();
        result.StudioScore.Should().BeNull();
        result.Basis.Should().Be(ScoreBasis.None);
        result.Categories.Should().HaveCount(11);
        result.Categories.Should().OnlyContain(c => c.Average == null && c.RatingCount == 0);
    }

    [Fact]
    public void Missing_data_is_null_and_never_zero()
    {
        var result = ScoreCalculator.Calculate([new ReviewRatings { Equipment = 4 }]);

        result.MembershipScore.Should().BeNull();
        result.Categories.Single(c => c.Category == RatingCategory.PriceValue).Average.Should().BeNull();
        result.Categories.Single(c => c.Category == RatingCategory.Equipment).Average.Should().Be(4);
        result.Basis.Should().Be(ScoreBasis.StudioOnly);
        result.TotalScore.Should().Be(4);
    }

    [Fact]
    public void Membership_only_reviews_use_membership_as_total()
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { PriceValue = 5, ContractTerms = 3 },
            new ReviewRatings { PriceValue = 4 },
        ]);

        result.Basis.Should().Be(ScoreBasis.MembershipOnly);
        // PriceValue avg = 4.5, ContractTerms avg = 3 -> membership = (4.5 + 3) / 2 = 3.75
        result.MembershipScore.Should().Be(3.75);
        result.TotalScore.Should().Be(3.75);
        result.StudioScore.Should().BeNull();
        result.ReviewCount.Should().Be(2);
    }

    [Fact]
    public void Both_areas_are_weighted_fifty_fifty()
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { PriceValue = 2, Equipment = 5, Cleanliness = 4 },
        ]);

        result.Basis.Should().Be(ScoreBasis.Both);
        result.MembershipScore.Should().Be(2);
        result.StudioScore.Should().Be(4.5); // (5 + 4) / 2
        result.TotalScore.Should().Be(3.25); // (2 + 4.5) / 2
    }

    [Fact]
    public void Area_average_uses_category_averages_not_raw_ratings()
    {
        // Equipment has 3 ratings (avg 5), Cleanliness has 1 rating (avg 1).
        // Area = mean of category averages = 3.0 (not the raw mean 4.0).
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { Equipment = 5 },
            new ReviewRatings { Equipment = 5 },
            new ReviewRatings { Equipment = 5, Cleanliness = 1 },
        ]);

        result.StudioScore.Should().Be(3.0);
    }

    [Fact]
    public void Averages_are_rounded_to_two_decimals()
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { Equipment = 5 },
            new ReviewRatings { Equipment = 4 },
            new ReviewRatings { Equipment = 4 },
        ]);

        result.Categories.Single(c => c.Category == RatingCategory.Equipment).Average.Should().Be(4.33);
        result.TotalScore.Should().Be(4.33);
    }

    [Fact]
    public void Category_counts_reflect_only_provided_ratings()
    {
        var result = ScoreCalculator.Calculate(
        [
            new ReviewRatings { Equipment = 4, Staff = 3 },
            new ReviewRatings { Equipment = 2 },
        ]);

        result.Categories.Single(c => c.Category == RatingCategory.Equipment).RatingCount.Should().Be(2);
        result.Categories.Single(c => c.Category == RatingCategory.Staff).RatingCount.Should().Be(1);
        result.Categories.Single(c => c.Category == RatingCategory.Showers).RatingCount.Should().Be(0);
    }

    [Fact]
    public void All_eleven_categories_are_split_into_membership_and_studio()
    {
        RatingCategories.Membership.Should().HaveCount(4);
        RatingCategories.Studio.Should().HaveCount(7);
        RatingCategories.Membership.Concat(RatingCategories.Studio)
            .Should().BeEquivalentTo(Enum.GetValues<RatingCategory>());
    }
}
