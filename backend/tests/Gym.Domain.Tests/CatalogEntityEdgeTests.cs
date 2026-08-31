using FluentAssertions;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class CatalogEntityEdgeTests
{
    public static TheoryData<int, bool> DistrictCases => new()
    {
        { 0, false },
        { 1, true },
        { 23, true },
        { 24, false },
    };

    public static TheoryData<GymStatus, bool, bool> StatusCases => new()
    {
        { GymStatus.Draft, false, false },
        { GymStatus.Active, true, true },
        { GymStatus.TemporarilyClosed, true, true },
        { GymStatus.PermanentlyClosed, true, false },
    };

    public static TheoryData<string, string> SlugCases => new()
    {
        { "Ärger Öfter Über Straße", "aerger-oefter-ueber-strasse" },
        { "  John   Harris!!! Wien  ", "john-harris-wien" },
        { "Café déjà vu", "cafe-deja-vu" },
        { "MIXED Case 123", "mixed-case-123" },
        { "***", "n-a" },
        { "Train & Fit + Yoga", "train-fit-yoga" },
    };

    public static TheoryData<int, int, int, bool> OpeningHourCases => new()
    {
        { 0, 8, 20, false },
        { 1, 8, 20, true },
        { 7, 8, 20, true },
        { 8, 8, 20, false },
        { 1, 8, 8, false },
        { 1, 20, 8, false },
    };

    [Theory]
    [MemberData(nameof(DistrictCases))]
    public void Gym_create_validates_vienna_district_boundaries(int district, bool expectedSuccess)
    {
        var result = GymEntry.Create("Gym", "gym", null, district, "Strasse 1", "1010", null, null, null, GymStatus.Active, DomainTestHelpers.Now);

        result.IsSuccess.Should().Be(expectedSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Gym_create_rejects_blank_name(string? name)
    {
        var result = GymEntry.Create(name!, "slug", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.Active, DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gym.name");
    }

    [Theory]
    [InlineData("", "1010")]
    [InlineData("   ", "1010")]
    [InlineData("Strasse", "")]
    [InlineData("Strasse", "   ")]
    public void Gym_create_rejects_missing_address_parts(string address, string postalCode)
    {
        var result = GymEntry.Create("Gym", "gym", null, 1, address, postalCode, null, null, null, GymStatus.Active, DomainTestHelpers.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("gym.address");
    }

    [Theory]
    [MemberData(nameof(StatusCases))]
    public void Gym_status_controls_visibility_and_review_acceptance(GymStatus status, bool visible, bool acceptsReviews)
    {
        var gym = GymEntry.Create("Gym", "gym", null, 1, "Strasse 1", "1010", null, null, null, status, DomainTestHelpers.Now).Value;

        gym.IsPubliclyVisible.Should().Be(visible);
        gym.AcceptsReviews.Should().Be(acceptsReviews);
    }

    [Theory]
    [MemberData(nameof(StatusCases))]
    public void Change_status_updates_timestamp_and_derived_flags(GymStatus status, bool visible, bool acceptsReviews)
    {
        var gym = GymEntry.Create("Gym", "gym", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.Draft, DomainTestHelpers.Now).Value;
        var changedAt = DomainTestHelpers.Now.AddHours(1);

        gym.ChangeStatus(status, changedAt);

        gym.Status.Should().Be(status);
        gym.UpdatedAtUtc.Should().Be(changedAt);
        gym.IsPubliclyVisible.Should().Be(visible);
        gym.AcceptsReviews.Should().Be(acceptsReviews);
    }

    [Theory]
    [MemberData(nameof(SlugCases))]
    public void Slug_generate_transliterates_and_normalizes_expected_inputs(string input, string expected)
    {
        Slug.Generate(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Slug_generate_rejects_null_or_whitespace(string? input)
    {
        Action act = () => Slug.Generate(input!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(OpeningHourCases))]
    public void Opening_hours_validate_day_and_range_boundaries(int day, int opensHour, int closesHour, bool expectedSuccess)
    {
        var result = GymOpeningHour.Create(day, new TimeOnly(opensHour, 0), new TimeOnly(closesHour, 0));

        result.IsSuccess.Should().Be(expectedSuccess);
    }

    [Fact]
    public void Gym_update_trims_and_sanitizes_mutable_fields_but_keeps_slug_stable()
    {
        var gym = GymEntry.Create("Old", "stable-slug", null, 1, "Alt 1", "1010", null, null, null, GymStatus.Active, DomainTestHelpers.Now).Value;
        var chainId = Guid.NewGuid();

        var result = gym.Update("  Neuer Name ", chainId, 23, " Neue Strasse 2 ", " 1230 ", " https://example.at ", "  +43 1 123 ", "  Text\u0000 ", DomainTestHelpers.Now.AddDays(1));

        result.IsSuccess.Should().BeTrue();
        gym.Name.Should().Be("Neuer Name");
        gym.Slug.Should().Be("stable-slug");
        gym.ChainId.Should().Be(chainId);
        gym.District.Should().Be(23);
        gym.AddressLine.Should().Be("Neue Strasse 2");
        gym.PostalCode.Should().Be("1230");
        gym.Description.Should().Be("Text");
    }

    [Fact]
    public void Set_amenities_deduplicates_ids_preserving_first_seen_order()
    {
        var gym = GymEntry.Create("Gym", "gym", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.Active, DomainTestHelpers.Now).Value;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        gym.SetAmenities([first, second, first], DomainTestHelpers.Now.AddMinutes(1));

        gym.AmenityIds.Should().Equal(first, second);
    }

    [Fact]
    public void Set_opening_hours_replaces_existing_hours()
    {
        var gym = GymEntry.Create("Gym", "gym", null, 1, "Strasse 1", "1010", null, null, null, GymStatus.Active, DomainTestHelpers.Now).Value;
        var monday = GymOpeningHour.Create(1, new TimeOnly(8, 0), new TimeOnly(20, 0)).Value;
        var tuesday = GymOpeningHour.Create(2, new TimeOnly(9, 0), new TimeOnly(21, 0)).Value;

        gym.SetOpeningHours([monday], DomainTestHelpers.Now.AddMinutes(1));
        gym.SetOpeningHours([tuesday], DomainTestHelpers.Now.AddMinutes(2));

        gym.OpeningHours.Should().ContainSingle().Which.IsoDayOfWeek.Should().Be(2);
    }

    [Fact]
    public void Chain_and_amenity_factories_trim_names_and_preserve_given_slug()
    {
        var chain = GymChain.Create("  Kette  ", "kette", "  https://example.at ", DomainTestHelpers.Now);
        var amenity = Amenity.Create("  Sauna  ", "sauna", DomainTestHelpers.Now);
        amenity.Rename("  Kurse  ");

        chain.Name.Should().Be("Kette");
        chain.Website.Should().Be("https://example.at");
        amenity.Name.Should().Be("Kurse");
        amenity.Slug.Should().Be("sauna");
    }
}
