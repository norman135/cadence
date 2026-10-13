using Cadence.Domain.Common;

namespace Cadence.Domain.Projects;

/// <summary>
/// A body of work inside an organization, such as a product or a team's area. Its <see cref="Key"/>
/// prefixes every issue key (<c>ATL-142</c>) and never changes, so links to issues stay valid.
/// </summary>
public sealed class Project : AggregateRoot<Guid>, ITenantScoped
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 80;
    public const int MinKeyLength = 2;
    public const int MaxKeyLength = 10;
    public const int MaxDescriptionLength = 2_000;

    private Project(Guid id, Guid organizationId, string key, string name, string? description, Guid createdByUserId, DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        Key = key;
        Name = name;
        Description = description;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    /// <summary>Used by EF Core.</summary>
    private Project()
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    public Guid OrganizationId { get; private init; }

    /// <summary>Uppercase letters and digits, starting with a letter, unique in the organization.</summary>
    public string Key { get; private init; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid CreatedByUserId { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Archived projects are read-only and hidden from navigation, but keep their issues.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>The number the most recent issue received. Issue numbers are allocated atomically in the database.</summary>
    public int LastIssueNumber { get; private init; }

    /// <summary>Issues not yet done or canceled. Denormalized, so navigation shows counts without counting.</summary>
    public int OpenIssueCount { get; private init; }

    public bool IsArchived => ArchivedAt is not null;

    public static Result<Project> Create(
        Guid organizationId,
        string key,
        string name,
        string? description,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        var normalizedKey = key.Trim().ToUpperInvariant();
        if (!IsValidKey(normalizedKey))
        {
            return ProjectErrors.InvalidKey;
        }

        var trimmedName = name.Trim();
        if (!IsValidName(trimmedName))
        {
            return ProjectErrors.InvalidName;
        }

        var normalizedDescription = NormalizeDescription(description);
        if (normalizedDescription?.Length > MaxDescriptionLength)
        {
            return ProjectErrors.DescriptionTooLong;
        }

        return new Project(Guid.CreateVersion7(), organizationId, normalizedKey, trimmedName, normalizedDescription, createdByUserId, now);
    }

    public Result Rename(string name)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return ProjectErrors.InvalidName;
        }

        Name = trimmed;
        return Result.Success();
    }

    public Result Describe(string? description)
    {
        var normalized = NormalizeDescription(description);
        if (normalized?.Length > MaxDescriptionLength)
        {
            return ProjectErrors.DescriptionTooLong;
        }

        Description = normalized;
        return Result.Success();
    }

    public void Archive(DateTimeOffset now) => ArchivedAt ??= now;

    public void Restore() => ArchivedAt = null;

    /// <summary>
    /// Suggests a key from a name: the initials of its words, or the first letters of a single word,
    /// e.g. "Atlas Mobile" becomes "AM" and "Atlas" becomes "ATL". Callers make it unique.
    /// </summary>
    public static string SuggestKey(string name)
    {
        var words = name
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string([.. word.Where(char.IsAsciiLetterOrDigit)]).ToUpperInvariant())
            .Where(word => word.Length > 0 && char.IsAsciiLetter(word[0]))
            .ToList();

        var key = words.Count switch
        {
            0 => "PRJ",
            1 => words[0][..Math.Min(3, words[0].Length)],
            _ => string.Concat(words.Take(MaxKeyLength).Select(word => word[0])),
        };

        return key.Length >= MinKeyLength ? key : (key + "X")[..MinKeyLength];
    }

    public static bool IsValidKey(string key) =>
        key.Length is >= MinKeyLength and <= MaxKeyLength
        && char.IsAsciiLetterUpper(key[0])
        && key.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character));

    private static bool IsValidName(string name) => name.Length is >= MinNameLength and <= MaxNameLength;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
