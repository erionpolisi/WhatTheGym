using FluentAssertions;
using FluentValidation.Results;
using Gym.Application.Contracts;
using Gym.Application.Features.Contact;
using Gym.Application.Features.Gyms;
using Gym.Application.Features.Legal;
using Gym.Application.Features.Reviews;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Xunit;

namespace Gym.Application.Tests;

public sealed class ReviewValidatorBoundaryTests
{
    public static TheoryData<RatingCategory, int, bool> RatingBoundaries()
    {
        var data = new TheoryData<RatingCategory, int, bool>();
        foreach (var category in Enum.GetValues<RatingCategory>())
        {
            foreach (var value in new[] { -1, 0, 1, 2, 5, 6, 99 })
            {
                data.Add(category, value, value is >= 1 and <= 5);
            }
        }

        return data;
    }

    public static TheoryData<string?, bool> TextBoundaries()
    {
        return new TheoryData<string?, bool>
        {
            { null, true },
            { string.Empty, true },
            { "   ", true },
            { "Solide.", true },
            { new string('a', Review.MaxTextLength - 1), true },
            { new string('a', Review.MaxTextLength), true },
            { new string('a', Review.MaxTextLength + 1), false },
            { "http://a.test http://b.test http://c.test", true },
            { "http://a.test http://b.test http://c.test http://d.test", false },
            { "HTTP://a.test HTTP://b.test HTTP://c.test HTTP://d.test", false },
        };
    }

    [Theory]
    [MemberData(nameof(RatingBoundaries))]
    public void Create_review_rating_boundaries_are_enforced(RatingCategory category, int value, bool expectedValid)
    {
        var command = new CreateReviewCommand(Guid.NewGuid(), "fit-studio", Rating(category, value), "Guter Eindruck.");

        var result = new CreateReviewCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid);
        AssertErrorsAreMeaningful(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(TextBoundaries))]
    public void Create_review_text_and_spam_boundaries_are_enforced(string? text, bool expectedValid)
    {
        var command = new CreateReviewCommand(Guid.NewGuid(), "fit-studio", Rating(RatingCategory.Equipment, 4), text);

        var result = new CreateReviewCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid);
        AssertErrorsAreMeaningful(result, expectedValid);
    }

    [Fact]
    public void Create_review_requires_at_least_one_rating_with_german_message()
    {
        var command = new CreateReviewCommand(Guid.NewGuid(), "fit-studio", EmptyRatings, null);

        var result = new CreateReviewCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("Mindestens", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("fit-studio", true)]
    public void Create_review_gym_slug_is_required(string? slug, bool expectedValid)
    {
        var command = new CreateReviewCommand(Guid.NewGuid(), slug!, Rating(RatingCategory.Equipment, 4), null);

        var result = new CreateReviewCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid);
        AssertErrorsAreMeaningful(result, expectedValid);
    }

    private static RatingsDto Rating(RatingCategory category, int value) => category switch
    {
        RatingCategory.PriceValue => EmptyRatings with { PriceValue = value },
        RatingCategory.ContractTerms => EmptyRatings with { ContractTerms = value },
        RatingCategory.Billing => EmptyRatings with { Billing = value },
        RatingCategory.CancellationExperience => EmptyRatings with { CancellationExperience = value },
        RatingCategory.Equipment => EmptyRatings with { Equipment = value },
        RatingCategory.Cleanliness => EmptyRatings with { Cleanliness = value },
        RatingCategory.Staff => EmptyRatings with { Staff = value },
        RatingCategory.Crowding => EmptyRatings with { Crowding = value },
        RatingCategory.ChangingRoom => EmptyRatings with { ChangingRoom = value },
        RatingCategory.Showers => EmptyRatings with { Showers = value },
        RatingCategory.Atmosphere => EmptyRatings with { Atmosphere = value },
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    private static readonly RatingsDto EmptyRatings = new(null, null, null, null, null, null, null, null, null, null, null);

    private static void AssertErrorsAreMeaningful(ValidationResult result, bool expectedValid)
    {
        if (expectedValid)
        {
            result.Errors.Should().BeEmpty();
        }
        else
        {
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
        }
    }
}

public sealed class GymValidatorBoundaryTests
{
    public static TheoryData<string, CreateGymCommand, bool> CreateGymCases() => BuildCreateGymCases();

    public static TheoryData<string, UpdateGymCommand, bool> UpdateGymCases()
    {
        var data = new TheoryData<string, UpdateGymCommand, bool>();
        foreach (var row in BuildCreateGymCases())
        {
            data.Add((string)row[0], ToUpdate((CreateGymCommand)row[1]), (bool)row[2]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CreateGymCases))]
    public void Create_gym_validator_enforces_boundaries(string scenario, CreateGymCommand command, bool expectedValid)
    {
        var result = new CreateGymCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid, scenario);
        AssertGermanValidationWhenInvalid(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(UpdateGymCases))]
    public void Update_gym_validator_enforces_boundaries(string scenario, UpdateGymCommand command, bool expectedValid)
    {
        var result = new UpdateGymCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid, scenario);
        AssertGermanValidationWhenInvalid(result, expectedValid);
    }

    [Fact]
    public void Create_gym_district_message_is_german()
    {
        var result = new CreateGymCommandValidator().Validate(ValidCreate() with { District = 24 });

        result.Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("Bezirk", StringComparison.Ordinal));
    }

    private static TheoryData<string, CreateGymCommand, bool> BuildCreateGymCases()
    {
        var data = new TheoryData<string, CreateGymCommand, bool>();
        foreach (var item in new (string Label, string? Value, bool Valid)[]
        {
            ("name-null", null, false), ("name-empty", string.Empty, false), ("name-space", "   ", false),
            ("name-min", "A", true), ("name-max", new string('a', 200), true), ("name-over", new string('a', 201), false),
        }) data.Add(item.Label, ValidCreate() with { Name = item.Value! }, item.Valid);

        foreach (var item in new (string Label, string? Value, bool Valid)[]
        {
            ("address-null", null, false), ("address-empty", string.Empty, false), ("address-space", "   ", false),
            ("address-min", "A", true), ("address-max", new string('a', 300), true), ("address-over", new string('a', 301), false),
        }) data.Add(item.Label, ValidCreate() with { AddressLine = item.Value! }, item.Valid);

        foreach (var item in new (string Label, int Value, bool Valid)[]
        {
            ("district-negative", -1, false), ("district-zero", 0, false), ("district-one", 1, true),
            ("district-middle", 12, true), ("district-max", 23, true), ("district-over", 24, false),
        }) data.Add(item.Label, ValidCreate() with { District = item.Value }, item.Valid);

        foreach (var item in new (string Label, string? Value, bool Valid)[]
        {
            ("postal-null", null, false), ("postal-empty", string.Empty, false), ("postal-space", "   ", false),
            ("postal-1000", "1000", true), ("postal-1234", "1234", true), ("postal-1999", "1999", true),
            ("postal-0999", "0999", false), ("postal-2000", "2000", false), ("postal-alpha", "1A00", false),
        }) data.Add(item.Label, ValidCreate() with { PostalCode = item.Value! }, item.Valid);

        foreach (var item in new (string Label, string? Value, bool Valid)[]
        {
            ("website-null", null, true), ("website-empty", string.Empty, true), ("website-space", "   ", true),
            ("website-http", "http://example.at", true), ("website-https", "https://example.at", true),
            ("website-relative", "/studio", false), ("website-ftp", "ftp://example.at", false), ("website-garbage", "not a url", false),
        }) data.Add(item.Label, ValidCreate() with { Website = item.Value }, item.Valid);

        foreach (var item in new (string Label, string? Value, bool Valid)[]
        {
            ("phone-null", null, true), ("phone-empty", string.Empty, true), ("phone-max", new string('1', 40), true), ("phone-over", new string('1', 41), false),
        }) data.Add(item.Label, ValidCreate() with { Phone = item.Value }, item.Valid);

        foreach (var item in new (string Label, string? Value, bool Valid)[]
        {
            ("description-null", null, true), ("description-empty", string.Empty, true), ("description-max", new string('d', 2000), true), ("description-over", new string('d', 2001), false),
        }) data.Add(item.Label, ValidCreate() with { Description = item.Value }, item.Valid);

        return data;
    }

    private static CreateGymCommand ValidCreate() => new("Fit Wien", null, 7, "Hauptstrasse 1", "1070", "https://example.at", "+43 1 234", "Beschreibung", "Active", [], []);

    private static UpdateGymCommand ToUpdate(CreateGymCommand c) => new(Guid.NewGuid(), c.Name, c.ChainId, c.District, c.AddressLine, c.PostalCode, c.Website, c.Phone, c.Description, c.AmenityIds, c.OpeningHours);

    private static void AssertGermanValidationWhenInvalid(ValidationResult result, bool expectedValid)
    {
        if (expectedValid)
        {
            result.Errors.Should().BeEmpty();
        }
        else
        {
            result.Errors.Should().NotBeEmpty();
            result.Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("ist", StringComparison.Ordinal) || m.Contains("muss", StringComparison.Ordinal) || m.Contains("darf", StringComparison.Ordinal));
        }
    }
}

public sealed class PublicFormValidatorBoundaryTests
{
    public static TheoryData<string, ReportReviewCommand, bool> ReportCases()
    {
        var data = new TheoryData<string, ReportReviewCommand, bool>();
        foreach (var item in NameCases(120)) data.Add($"report-{item.Label}", ValidReport() with { ReporterName = item.Value! }, item.Valid);
        foreach (var item in EmailCases()) data.Add($"report-{item.Label}", ValidReport() with { ReporterEmail = item.Value! }, item.Valid);
        foreach (var item in TextCases(20, LegalCase.MaxDescriptionLength)) data.Add($"report-{item.Label}", ValidReport() with { Description = item.Value! }, item.Valid);
        return data;
    }

    public static TheoryData<string, CreateContactRequestCommand, bool> ContactCases()
    {
        var data = new TheoryData<string, CreateContactRequestCommand, bool>();
        foreach (var item in NameCases(120)) data.Add($"contact-{item.Label}", ValidContact() with { Name = item.Value! }, item.Valid);
        foreach (var item in EmailCases()) data.Add($"contact-{item.Label}", ValidContact() with { Email = item.Value! }, item.Valid);
        foreach (var item in TextCases(10, ContactRequest.MaxMessageLength)) data.Add($"contact-{item.Label}", ValidContact() with { Message = item.Value! }, item.Valid);
        foreach (var item in LinkCases()) data.Add($"contact-{item.Label}", ValidContact() with { Message = item.Value }, item.Valid);
        return data;
    }

    [Theory]
    [MemberData(nameof(ReportCases))]
    public void Report_review_validator_enforces_boundaries(string scenario, ReportReviewCommand command, bool expectedValid)
    {
        var result = new ReportReviewCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid, scenario);
        AssertValidationResult(result, expectedValid);
    }

    [Theory]
    [MemberData(nameof(ContactCases))]
    public void Contact_request_validator_enforces_boundaries(string scenario, CreateContactRequestCommand command, bool expectedValid)
    {
        var result = new CreateContactRequestCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid, scenario);
        AssertValidationResult(result, expectedValid);
    }

    [Theory]
    [InlineData("report")]
    [InlineData("contact")]
    public void Public_form_email_messages_are_german(string form)
    {
        var errors = form == "report"
            ? new ReportReviewCommandValidator().Validate(ValidReport() with { ReporterEmail = "keine-email" }).Errors
            : new CreateContactRequestCommandValidator().Validate(ValidContact() with { Email = "keine-email" }).Errors;

        errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("gültig", StringComparison.Ordinal) || m.Contains("gueltig", StringComparison.Ordinal));
    }

    private static ReportReviewCommand ValidReport() => new(Guid.NewGuid(), "Defamation", "Melderin", "melderin@example.at", new string('b', 20));

    private static CreateContactRequestCommand ValidContact() => new("General", "Melderin", "melderin@example.at", new string('c', 10), null);

    private static IEnumerable<(string Label, string? Value, bool Valid)> NameCases(int max)
    {
        yield return ("name-null", null, false);
        yield return ("name-empty", string.Empty, false);
        yield return ("name-space", "   ", false);
        yield return ("name-min", "A", true);
        yield return ("name-trimmed", "  Anna  ", true);
        yield return ("name-max", new string('n', max), true);
        yield return ("name-over", new string('n', max + 1), false);
    }

    private static IEnumerable<(string Label, string? Value, bool Valid)> EmailCases()
    {
        yield return ("email-null", null, false);
        yield return ("email-empty", string.Empty, false);
        yield return ("email-space", "   ", false);
        yield return ("email-simple", "a@example.at", true);
        yield return ("email-plus", "anna+test@example.at", true);
        yield return ("email-subdomain", "anna@test.example.at", true);
        yield return ("email-localhost", "anna@localhost", true);
        yield return ("email-missing-at", "anna.example.at", false);
        yield return ("email-missing-domain", "anna@", false);
        yield return ("email-over", $"{new string('a', 245)}@example.at", false);
    }

    private static IEnumerable<(string Label, string? Value, bool Valid)> TextCases(int min, int max)
    {
        yield return ("text-null", null, false);
        yield return ("text-empty", string.Empty, false);
        yield return ("text-space", "   ", false);
        yield return ("text-min-minus-one", new string('t', min - 1), false);
        yield return ("text-min", new string('t', min), true);
        yield return ("text-middle", new string('t', min + 25), true);
        yield return ("text-max-minus-one", new string('t', max - 1), true);
        yield return ("text-max", new string('t', max), true);
        yield return ("text-over", new string('t', max + 1), false);
    }

    private static IEnumerable<(string Label, string Value, bool Valid)> LinkCases()
    {
        yield return ("links-three", "https://a.at https://b.at https://c.at", true);
        yield return ("links-four", "https://a.at https://b.at https://c.at https://d.at", false);
        yield return ("links-http-four", "http://a.at http://b.at http://c.at http://d.at", false);
    }

    private static void AssertValidationResult(ValidationResult result, bool expectedValid)
    {
        if (expectedValid)
        {
            result.Errors.Should().BeEmpty();
        }
        else
        {
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
        }
    }
}

public sealed class ValidatorGapBugReportTests
{
    [Fact]
    public void Gym_validators_should_reject_null_postal_code()
    {
        new CreateGymCommandValidator().Validate(new CreateGymCommand("Fit", null, 1, "Adresse", null!, null, null, null, "Active", [], [])).IsValid.Should().BeFalse();
        new UpdateGymCommandValidator().Validate(new UpdateGymCommand(Guid.NewGuid(), "Fit", null, 1, "Adresse", null!, null, null, null, [], [])).IsValid.Should().BeFalse();
    }
}
