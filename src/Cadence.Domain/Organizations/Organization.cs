using System.Text;
using Cadence.Domain.Common;

namespace Cadence.Domain.Organizations;

/// <summary>A workspace that owns projects and has members. The tenant boundary of Cadence.</summary>
public sealed class Organization : AggregateRoot<Guid>
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 80;
    public const int MaxSlugLength = 48;

    private Organization(Guid id, string name, string slug, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    /// <summary>Used by EF Core.</summary>
    private Organization()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    public string Name { get; private set; }

    /// <summary>URL-friendly, globally unique identifier, e.g. <c>acme-corp</c>. Never changes.</summary>
    public string Slug { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public static Result<Organization> Create(string name, string slug, DateTimeOffset now)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return OrganizationErrors.InvalidName;
        }

        return new Organization(Guid.CreateVersion7(), trimmed, slug, now);
    }

    public Result Rename(string name)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return OrganizationErrors.InvalidName;
        }

        Name = trimmed;
        return Result.Success();
    }

    /// <summary>
    /// Derives a slug from a name: lowercase ASCII letters and digits separated by single hyphens,
    /// e.g. "Acme Corp!" becomes "acme-corp". Callers make it unique.
    /// </summary>
    public static string SlugFrom(string name)
    {
        var slug = new StringBuilder(MaxSlugLength);
        var pendingHyphen = false;

        foreach (var character in name.ToLowerInvariant())
        {
            var ascii = char.IsAsciiLetterOrDigit(character) ? character.ToString() : Transliterate(character);
            if (ascii is null)
            {
                pendingHyphen = true;
                continue;
            }

            if (pendingHyphen && slug.Length > 0)
            {
                slug.Append('-');
            }

            slug.Append(ascii);
            pendingHyphen = false;

            if (slug.Length >= MaxSlugLength - 5)
            {
                break;
            }
        }

        return slug.Length == 0 ? "org" : slug.ToString(0, Math.Min(slug.Length, MaxSlugLength - 5)).TrimEnd('-');
    }

    /// <summary>
    /// ASCII spellings of common accented Latin letters. The runtime is culture-invariant (no ICU),
    /// so Unicode decomposition can't be relied on for this.
    /// </summary>
    private static string? Transliterate(char character) => character switch
    {
        'à' or 'á' or 'â' or 'ã' or 'ä' or 'å' or 'ā' or 'ă' or 'ą' => "a",
        'ç' or 'ć' or 'č' => "c",
        'ď' or 'đ' => "d",
        'è' or 'é' or 'ê' or 'ë' or 'ē' or 'ę' or 'ě' => "e",
        'ì' or 'í' or 'î' or 'ï' or 'ī' => "i",
        'ł' => "l",
        'ñ' or 'ń' or 'ň' => "n",
        'ò' or 'ó' or 'ô' or 'õ' or 'ö' or 'ø' or 'ō' or 'ő' => "o",
        'ř' => "r",
        'ś' or 'š' => "s",
        'ť' => "t",
        'ù' or 'ú' or 'û' or 'ü' or 'ū' or 'ů' or 'ű' => "u",
        'ý' or 'ÿ' => "y",
        'ź' or 'ż' or 'ž' => "z",
        'ß' => "ss",
        'æ' => "ae",
        'œ' => "oe",
        _ => null,
    };

    private static bool IsValidName(string name) => name.Length is >= MinNameLength and <= MaxNameLength;
}
