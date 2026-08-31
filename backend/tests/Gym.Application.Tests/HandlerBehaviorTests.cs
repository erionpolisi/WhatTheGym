using FluentAssertions;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Amenities;
using Gym.Application.Features.Analytics;
using Gym.Application.Features.Chains;
using Gym.Application.Features.Contact;
using Gym.Application.Features.Gyms;
using Gym.Application.Features.Legal;
using Gym.Application.Features.Reviews;
using Gym.Application.Features.Users;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gym.Application.Tests;

public sealed class PagingAndSearchHandlerTests
{
    public static TheoryData<int?, int?, int, int> PagingCases() => new()
    {
        { null, null, 1, 20 },
        { 0, null, 1, 20 },
        { -1, null, 1, 20 },
        { 1, null, 1, 20 },
        { 2, null, 2, 20 },
        { null, 0, 1, 1 },
        { null, -5, 1, 1 },
        { null, 1, 1, 1 },
        { null, 20, 1, 20 },
        { null, 100, 1, 100 },
        { null, 101, 1, 100 },
        { 0, 0, 1, 1 },
        { -9, 200, 1, 100 },
        { 3, 5, 3, 5 },
        { int.MaxValue, int.MaxValue, int.MaxValue, 100 },
    };

    public static TheoryData<int?, bool> DistrictCases() => new()
    {
        { null, true }, { -1, false }, { 0, false }, { 1, true }, { 7, true }, { 23, true }, { 24, false }, { 99, false },
    };

    [Theory]
    [MemberData(nameof(PagingCases))]
    public void Paging_normalize_clamps_bounds(int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var normalized = Paging.Normalize(page, pageSize);

        normalized.Page.Should().Be(expectedPage);
        normalized.PageSize.Should().Be(expectedPageSize);
    }

    [Theory]
    [MemberData(nameof(DistrictCases))]
    public async Task Search_gyms_enforces_vienna_district_boundaries(int? district, bool expectedSuccess)
    {
        var search = new AppFakeGymSearchQuery();
        var sut = new SearchGymsQueryHandler(search);

        var result = await sut.Handle(new SearchGymsQuery("  kraft  ", district, "  chain  ", 3, 2, 1, "score", 0, 150), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            search.LastCriteria.Should().NotBeNull();
            search.LastCriteria!.Term.Should().Be("kraft");
            search.LastCriteria.ChainSlug.Should().Be("chain");
            search.LastCriteria.Page.Should().Be(1);
            search.LastCriteria.PageSize.Should().Be(100);
        }
        else
        {
            result.Error.Code.Should().Be("search.district");
            result.Error.Message.Should().Contain("Bezirk");
        }
    }

    [Fact]
    public async Task Search_gyms_maps_rows_to_contract()
    {
        var id = Guid.NewGuid();
        var search = new AppFakeGymSearchQuery();
        search.Rows.Add(new GymSearchRow(id, "Fit", "fit", 2, "Adresse", "1020", GymStatus.Active, "Kette", "kette", 3, 4.2, 4.1, 4.3, ScoreBasis.Both));

        var result = await new SearchGymsQueryHandler(search).Handle(new SearchGymsQuery(null, null, null, null, null, null, null, 1, 20), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new GymListItemDto(id, "Fit", "fit", 2, "Adresse", "1020", "Active", "Kette", "kette", 3, 4.2, 4.1, 4.3, "both"));
    }
}

public sealed class CatalogHandlerBehaviorTests
{
    public static TheoryData<string?, bool> ChainNameCases() => new()
    {
        { null, false }, { string.Empty, false }, { "   ", false }, { "A", true }, { new string('c', 200), true }, { new string('c', 201), false },
    };

    public static TheoryData<string?, bool> AmenityNameCases() => new()
    {
        { null, false }, { string.Empty, false }, { "   ", false }, { "WLAN", true }, { new string('a', 120), true }, { new string('a', 121), false },
    };

    public static TheoryData<string, bool> GymStatusCases() => new()
    {
        { "Active", true }, { "active", true }, { "Draft", true }, { "TemporarilyClosed", true }, { "PermanentlyClosed", true },
        { "", false }, { " ", false }, { "Archived", false }, { "Open", false }, { "1", true },
    };

    [Theory]
    [MemberData(nameof(ChainNameCases))]
    public async Task Create_chain_validates_name_and_generates_unique_slug(string? name, bool expectedSuccess)
    {
        var chains = new AppFakeChainRepository();
        chains.ExistingSlugs.Add("fit-wien");
        chains.ExistingSlugs.Add("fit-wien-2");
        var unit = new FakeUnitOfWork();
        var sut = new CreateChainCommandHandler(chains, unit, new FakeClock(AppTestData.Now));

        var result = await sut.Handle(new CreateChainCommand(name!, "https://example.at"), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            chains.Chains.Single().Name.Should().Be(name!.Trim());
            unit.SaveCount.Should().Be(1);
        }
        else
        {
            result.Error.Code.Should().Be("chain.name");
        }
    }

    [Fact]
    public async Task Create_chain_collision_appends_numeric_suffix()
    {
        var chains = new AppFakeChainRepository();
        chains.ExistingSlugs.UnionWith(["fit-wien", "fit-wien-2"]);

        await new CreateChainCommandHandler(chains, new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new CreateChainCommand("Fit Wien", null), CancellationToken.None);

        chains.Chains.Single().Slug.Should().Be("fit-wien-3");
    }

    [Theory]
    [MemberData(nameof(AmenityNameCases))]
    public async Task Create_amenity_validates_name_and_generates_slug(string? name, bool expectedSuccess)
    {
        var amenities = new AppFakeAmenityRepository();
        amenities.ExistingSlugs.UnionWith(["sauna", "sauna-2"]);
        var unit = new FakeUnitOfWork();
        var sut = new CreateAmenityCommandHandler(amenities, unit, new FakeClock(AppTestData.Now));

        var result = await sut.Handle(new CreateAmenityCommand(name!), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            amenities.Amenities.Single().Name.Should().Be(name!.Trim());
            unit.SaveCount.Should().Be(1);
        }
        else
        {
            result.Error.Code.Should().Be("amenity.name");
        }
    }

    [Fact]
    public async Task Create_amenity_collision_appends_numeric_suffix()
    {
        var amenities = new AppFakeAmenityRepository();
        amenities.ExistingSlugs.UnionWith(["sauna", "sauna-2", "sauna-3"]);

        await new CreateAmenityCommandHandler(amenities, new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new CreateAmenityCommand("Sauna"), CancellationToken.None);

        amenities.Amenities.Single().Slug.Should().Be("sauna-4");
    }

    [Theory]
    [MemberData(nameof(GymStatusCases))]
    public async Task Change_gym_status_rejects_garbage_and_accepts_known_values(string status, bool expectedSuccess)
    {
        var gyms = new InMemoryGymRepository();
        var gym = AppTestData.Gym();
        gyms.Add(gym);
        var result = await new ChangeGymStatusCommandHandler(gyms, new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new ChangeGymStatusCommand(gym.Id, status), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (!expectedSuccess)
        {
            result.Error.Type.Should().Be(ErrorType.Validation);
            result.Error.Message.Should().Contain("Ungueltig");
        }
    }

    [Fact]
    public async Task Delete_chain_in_use_returns_conflict()
    {
        var chains = new AppFakeChainRepository();
        var chain = GymChain.Create("Kette", "kette", null, AppTestData.Now);
        chains.Add(chain);
        chains.GymCounts[chain.Id] = 1;

        var result = await new DeleteChainCommandHandler(chains, new FakeUnitOfWork()).Handle(new DeleteChainCommand(chain.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("chain.inUse");
    }

    [Fact]
    public async Task Delete_amenity_in_use_returns_conflict()
    {
        var amenities = new AppFakeAmenityRepository();
        var amenity = Amenity.Create("Sauna", "sauna", AppTestData.Now);
        amenities.Add(amenity);
        amenities.GymCounts[amenity.Id] = 1;

        var result = await new DeleteAmenityCommandHandler(amenities, new FakeUnitOfWork()).Handle(new DeleteAmenityCommand(amenity.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("amenity.inUse");
    }

    [Fact]
    public async Task Create_gym_indexes_after_successful_save_and_maps_amenities_hours()
    {
        var gyms = new InMemoryGymRepository();
        var amenities = new AppFakeAmenityRepository();
        var amenity = Amenity.Create("Duschen", "duschen", AppTestData.Now);
        amenities.Add(amenity);
        var search = new FakeSearchIndex();
        var unit = new FakeUnitOfWork();
        var command = new CreateGymCommand("Neues Gym", null, 5, "Gasse 1", "1050", null, null, null, "Active", [amenity.Id], [new OpeningHourInput(1, "08:00", "20:00")]);

        var result = await new CreateGymCommandHandler(gyms, new AppFakeChainRepository(), amenities, search, unit, new FakeClock(AppTestData.Now), new CreateGymCommandValidator())
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        gyms.Gyms.Single().AmenityIds.Should().Equal([amenity.Id]);
        gyms.Gyms.Single().OpeningHours.Should().ContainSingle(h => h.IsoDayOfWeek == 1);
        unit.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData("25:00", "20:00", "openingHours.format")]
    [InlineData("08:00", "08:00", "openingHours.range")]
    [InlineData("08:00", "07:59", "openingHours.range")]
    public async Task Create_gym_rejects_invalid_opening_hours(string opensAt, string closesAt, string expectedCode)
    {
        var command = new CreateGymCommand("Neues Gym", null, 5, "Gasse 1", "1050", null, null, null, "Active", [], [new OpeningHourInput(1, opensAt, closesAt)]);

        var result = await new CreateGymCommandHandler(new InMemoryGymRepository(), new AppFakeChainRepository(), new AppFakeAmenityRepository(), new FakeSearchIndex(), new FakeUnitOfWork(), new FakeClock(AppTestData.Now), new CreateGymCommandValidator())
            .Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }
}

public sealed class ReviewHandlerBehaviorTests
{
    public static TheoryData<string, bool> ModerationStatusCases() => new()
    {
        { "Published", true }, { "published", true }, { "SoftDeleted", true }, { "UnderReview", true }, { "RemovedLegal", true },
        { "", false }, { " ", false }, { "Deleted", false }, { "Garbage", false }, { "1", true },
    };

    [Theory]
    [MemberData(nameof(ModerationStatusCases))]
    public async Task Moderation_queue_parses_status_and_clamps_paging(string status, bool expectedSuccess)
    {
        var reviews = new InMemoryReviewRepository();
        reviews.Add(AppTestData.Review(status: ReviewStatus.Published));

        var result = await new ModerationQueueQueryHandler(reviews).Handle(new ModerationQueueQuery(status, 0, 101), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            result.Value.Page.Should().Be(1);
            result.Value.PageSize.Should().Be(100);
        }
        else
        {
            result.Error.Code.Should().Be("moderation.status");
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Duplikat und beleidigend", true)]
    public async Task Moderator_remove_requires_reason(string? reason, bool expectedSuccess)
    {
        var reviews = new InMemoryReviewRepository();
        var summaries = new InMemorySummaryStore();
        var review = AppTestData.Review();
        reviews.Add(review);

        var result = await new ModeratorRemoveReviewCommandHandler(reviews, new GymScoreUpdater(reviews, summaries), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new ModeratorRemoveReviewCommand(Guid.NewGuid(), UserRole.Moderator, review.Id, reason!), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            review.Status.Should().Be(ReviewStatus.SoftDeleted);
            review.DeletionOrigin.Should().Be(ReviewDeletionOrigin.Moderator);
            summaries.Scores.Should().ContainKey(review.GymId);
        }
        else
        {
            result.Error.Code.Should().Be("moderation.reason");
        }
    }

    [Fact]
    public async Task Admin_remove_uses_admin_origin_and_recalculates_score()
    {
        var reviews = new InMemoryReviewRepository();
        var summaries = new InMemorySummaryStore();
        var review = AppTestData.Review();
        reviews.Add(review);

        var result = await new ModeratorRemoveReviewCommandHandler(reviews, new GymScoreUpdater(reviews, summaries), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new ModeratorRemoveReviewCommand(Guid.NewGuid(), UserRole.Admin, review.Id, "Rechtsgrund"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        review.DeletionOrigin.Should().Be(ReviewDeletionOrigin.Admin);
        summaries.Scores[review.GymId].ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task Only_author_can_update_own_review()
    {
        var reviews = new InMemoryReviewRepository();
        var review = AppTestData.Review();
        reviews.Add(review);

        var result = await new UpdateOwnReviewCommandHandler(reviews, new GymScoreUpdater(reviews, new InMemorySummaryStore()), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new UpdateOwnReviewCommand(Guid.NewGuid(), review.Id, AppTestData.Ratings(5), "Neu"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task Update_own_review_uses_domain_rating_bounds_and_recalculates(int rating, bool expectedSuccess)
    {
        var reviews = new InMemoryReviewRepository();
        var summaries = new InMemorySummaryStore();
        var review = AppTestData.Review();
        reviews.Add(review);

        var result = await new UpdateOwnReviewCommandHandler(reviews, new GymScoreUpdater(reviews, summaries), new FakeUnitOfWork(), new FakeClock(AppTestData.Now.AddHours(1)))
            .Handle(new UpdateOwnReviewCommand(review.UserId, review.Id, AppTestData.Ratings(rating), "Neu"), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            reviews.Revisions.Should().ContainSingle(r => r.ReviewId == review.Id);
            summaries.Scores.Should().ContainKey(review.GymId);
        }
        else
        {
            result.Error.Type.Should().Be(ErrorType.Validation);
        }
    }

    [Fact]
    public async Task Deleting_own_review_soft_deletes_and_recalculates()
    {
        var reviews = new InMemoryReviewRepository();
        var summaries = new InMemorySummaryStore();
        var review = AppTestData.Review();
        reviews.Add(review);

        var result = await new DeleteOwnReviewCommandHandler(reviews, new GymScoreUpdater(reviews, summaries), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new DeleteOwnReviewCommand(review.UserId, review.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.SoftDeleted);
        summaries.Scores[review.GymId].ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task User_role_must_not_remove_reviews()
    {
        var reviews = new InMemoryReviewRepository();
        var review = AppTestData.Review();
        reviews.Add(review);

        var result = await new ModeratorRemoveReviewCommandHandler(reviews, new GymScoreUpdater(reviews, new InMemorySummaryStore()), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new ModeratorRemoveReviewCommand(Guid.NewGuid(), UserRole.User, review.Id, "Keine Berechtigung"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}

public sealed class ContactAnalyticsAndLegalDocumentHandlerTests
{
    public static TheoryData<string, bool> ContactTypeCases() => new()
    {
        { "General", true }, { "general", true }, { "GymSuggestion", true }, { "DataCorrection", true }, { "", false }, { " ", false }, { "Spam", false }, { "1", true },
    };

    public static TheoryData<string, bool> ContactStatusCases() => new()
    {
        { "New", true }, { "new", true }, { "InProgress", true }, { "Resolved", true }, { "", false }, { "Done", false }, { "1", true },
    };

    public static TheoryData<string, bool> LegalDocumentTypeCases() => new()
    {
        { "Imprint", true }, { "imprint", true }, { "PrivacyPolicy", true }, { "TermsOfUse", true }, { "", false }, { "CookiePolicy", false }, { "1", true },
    };

    public static TheoryData<string, bool> AnalyticsEventCases() => new()
    {
        { "page_view", true }, { "search_performed", true }, { "gym_detail_view", true }, { "review_created", true }, { "report_submitted", true }, { "contact_submitted", true },
        { "Page_View", false }, { "login", false }, { "", false }, { " ", false }, { "<script>", false },
    };

    public static TheoryData<string?, bool> AnalyticsSessionCases() => new()
    {
        { null, false }, { string.Empty, false }, { "   ", false }, { "abc", true }, { new string('s', 128), true }, { new string('s', 129), false },
    };

    [Theory]
    [MemberData(nameof(ContactTypeCases))]
    public async Task Create_contact_request_parses_type_and_queues_confirmation(string type, bool expectedSuccess)
    {
        var contacts = new AppFakeContactRepository();
        var outbox = new FakeOutbox();
        var result = await new CreateContactRequestCommandHandler(contacts, new InMemoryGymRepository(), outbox, new FakeUnitOfWork(), new FakeClock(AppTestData.Now), new CreateContactRequestCommandValidator())
            .Handle(new CreateContactRequestCommand(type, "Anna", "anna@example.at", "Bitte Studio pruefen.", null), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            contacts.Requests.Should().ContainSingle();
            outbox.Sent.Single().Subject.Should().StartWith("[WhatTheGym] Ihre Anfrage");
        }
        else
        {
            result.Error.Code.Should().Be("contact.type");
        }
    }

    [Theory]
    [MemberData(nameof(ContactStatusCases))]
    public async Task Contact_status_command_parses_status(string status, bool expectedSuccess)
    {
        var contacts = new AppFakeContactRepository();
        var request = ContactRequest.Create(ContactRequestType.General, "Anna", "anna@example.at", "Bitte melden.", null, AppTestData.Now).Value;
        contacts.Add(request);

        var result = await new SetContactRequestStatusCommandHandler(contacts, new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new SetContactRequestStatusCommand(request.Id, status), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (!expectedSuccess)
        {
            result.Error.Type.Should().Be(ErrorType.Validation);
        }
    }

    [Theory]
    [MemberData(nameof(LegalDocumentTypeCases))]
    public async Task Create_legal_document_parses_type_and_requires_content(string type, bool expectedSuccess)
    {
        var docs = new AppFakeLegalDocumentRepository();
        var result = await new CreateLegalDocumentVersionCommandHandler(docs, new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new CreateLegalDocumentVersionCommand(type, "Titel", "ENTWURF - anwaltlich pruefen lassen"), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            docs.Documents.Single().Version.Should().Be(1);
        }
        else
        {
            result.Error.Code.Should().Be("legalDocument.type");
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Titel", true)]
    public async Task Create_legal_document_requires_title(string? title, bool expectedSuccess)
    {
        var result = await new CreateLegalDocumentVersionCommandHandler(new AppFakeLegalDocumentRepository(), new FakeUnitOfWork(), new FakeClock(AppTestData.Now))
            .Handle(new CreateLegalDocumentVersionCommand("Imprint", title!, "Inhalt"), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
    }

    [Theory]
    [MemberData(nameof(AnalyticsEventCases))]
    public async Task Analytics_event_type_allowlist_is_enforced(string eventType, bool expectedSuccess)
    {
        var store = new AppFakeAnalyticsStore();
        var result = await AnalyticsSut(store).Handle(new RecordAnalyticsEventCommand(eventType, "/studio?email=a@example.at#x", "session-1"), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            store.Events.Single().Path.Should().Be("/studio");
            store.Events.Single().SessionBucket.Should().Be("hashed:session-1");
        }
        else
        {
            result.Error.Code.Should().Be("analytics.eventType");
        }
    }

    [Theory]
    [MemberData(nameof(AnalyticsSessionCases))]
    public async Task Analytics_session_boundary_is_enforced(string? sessionId, bool expectedSuccess)
    {
        var store = new AppFakeAnalyticsStore();
        var result = await AnalyticsSut(store).Handle(new RecordAnalyticsEventCommand("page_view", "/", sessionId!), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
        store.Events.Count.Should().Be(expectedSuccess ? 1 : 0);
    }

    [Fact]
    public async Task Analytics_path_is_capped_to_200_without_query_or_fragment()
    {
        var store = new AppFakeAnalyticsStore();
        var longPath = "/" + new string('a', 250) + "?token=secret#fragment";

        var result = await AnalyticsSut(store).Handle(new RecordAnalyticsEventCommand("page_view", longPath, "session"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Events.Single().Path.Should().HaveLength(200);
        store.Events.Single().Path.Should().NotContain("token");
    }

    private static RecordAnalyticsEventCommandHandler AnalyticsSut(AppFakeAnalyticsStore store) =>
        new(store, new AppFakeSessionBucketHasher(), new FakeUnitOfWork(), new FakeClock(AppTestData.Now), Options.Create(new AnalyticsOptions()));
}



