using Gym.Domain.Common;
using Gym.Domain.Enums;

namespace Gym.Domain.Entities;

public sealed class GymChain : Entity
{
    private GymChain()
    {
        Name = null!;
        Slug = null!;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public string? Website { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static GymChain Create(string name, string slug, string? website, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Slug = slug,
        Website = TextSanitizer.Sanitize(website),
        CreatedAtUtc = utcNow,
        UpdatedAtUtc = utcNow,
    };

    public void Update(string name, string? website, DateTimeOffset utcNow)
    {
        Name = name.Trim();
        Website = TextSanitizer.Sanitize(website);
        UpdatedAtUtc = utcNow;
    }
}

public sealed class Amenity : Entity
{
    private Amenity()
    {
        Name = null!;
        Slug = null!;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Amenity Create(string name, string slug, DateTimeOffset utcNow) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Slug = slug,
        CreatedAtUtc = utcNow,
    };

    public void Rename(string name) => Name = name.Trim();
}

public sealed class GymOpeningHour
{
    private GymOpeningHour()
    {
    }

    public Guid GymId { get; private set; }

    /// <summary>ISO 8601 day of week: 1 = Monday .. 7 = Sunday.</summary>
    public int IsoDayOfWeek { get; private set; }

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    public static Result<GymOpeningHour> Create(int isoDayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (isoDayOfWeek is < 1 or > 7)
        {
            return Result.Failure<GymOpeningHour>(Error.Validation("openingHours.day", "Day of week must be 1 (Monday) to 7 (Sunday)."));
        }

        if (closesAt <= opensAt)
        {
            return Result.Failure<GymOpeningHour>(Error.Validation("openingHours.range", "Closing time must be after opening time."));
        }

        return new GymOpeningHour { IsoDayOfWeek = isoDayOfWeek, OpensAt = opensAt, ClosesAt = closesAt };
    }
}

public sealed class GymEntry : Entity
{
    public const int MinDistrict = 1;
    public const int MaxDistrict = 23;

    private readonly List<GymOpeningHour> _openingHours = [];

    private GymEntry()
    {
        Name = null!;
        Slug = null!;
        AddressLine = null!;
        PostalCode = null!;
        City = null!;
        CountryCode = null!;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public Guid? ChainId { get; private set; }

    public GymChain? Chain { get; private set; }

    /// <summary>Vienna district 1-23.</summary>
    public int District { get; private set; }

    public string AddressLine { get; private set; }

    public string PostalCode { get; private set; }

    public string City { get; private set; }

    public string CountryCode { get; private set; }

    public string? Website { get; private set; }

    public string? Phone { get; private set; }

    public string? Description { get; private set; }

    public GymStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public List<Guid> AmenityIds { get; private set; } = [];

    public IReadOnlyCollection<GymOpeningHour> OpeningHours => _openingHours;

    public bool IsPubliclyVisible => Status != GymStatus.Draft;

    public bool AcceptsReviews => Status is GymStatus.Active or GymStatus.TemporarilyClosed;

    public static Result<GymEntry> Create(
        string name,
        string slug,
        Guid? chainId,
        int district,
        string addressLine,
        string postalCode,
        string? website,
        string? phone,
        string? description,
        GymStatus status,
        DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<GymEntry>(Error.Validation("gym.name", "Name is required."));
        }

        if (district is < MinDistrict or > MaxDistrict)
        {
            return Result.Failure<GymEntry>(Error.Validation("gym.district", "District must be between 1 and 23."));
        }

        if (string.IsNullOrWhiteSpace(addressLine) || string.IsNullOrWhiteSpace(postalCode))
        {
            return Result.Failure<GymEntry>(Error.Validation("gym.address", "Address line and postal code are required."));
        }

        return new GymEntry
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug,
            ChainId = chainId,
            District = district,
            AddressLine = addressLine.Trim(),
            PostalCode = postalCode.Trim(),
            City = "Wien",
            CountryCode = "AT",
            Website = TextSanitizer.Sanitize(website),
            Phone = TextSanitizer.Sanitize(phone),
            Description = TextSanitizer.Sanitize(description),
            Status = status,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public Result Update(
        string name,
        Guid? chainId,
        int district,
        string addressLine,
        string postalCode,
        string? website,
        string? phone,
        string? description,
        DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("gym.name", "Name is required."));
        }

        if (district is < MinDistrict or > MaxDistrict)
        {
            return Result.Failure(Error.Validation("gym.district", "District must be between 1 and 23."));
        }

        Name = name.Trim();
        ChainId = chainId;
        District = district;
        AddressLine = addressLine.Trim();
        PostalCode = postalCode.Trim();
        Website = TextSanitizer.Sanitize(website);
        Phone = TextSanitizer.Sanitize(phone);
        Description = TextSanitizer.Sanitize(description);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    public void ChangeStatus(GymStatus status, DateTimeOffset utcNow)
    {
        Status = status;
        UpdatedAtUtc = utcNow;
    }

    public void SetAmenities(IEnumerable<Guid> amenityIds, DateTimeOffset utcNow)
    {
        AmenityIds = amenityIds.Distinct().ToList();
        UpdatedAtUtc = utcNow;
    }

    public void SetOpeningHours(IEnumerable<GymOpeningHour> hours, DateTimeOffset utcNow)
    {
        _openingHours.Clear();
        _openingHours.AddRange(hours);
        UpdatedAtUtc = utcNow;
    }
}
