using Cadence.Domain.Common;

namespace Cadence.Domain.Issues;

/// <summary>The nine label colors of the design system (design board 02).</summary>
public enum LabelColor
{
    Teal,
    Blue,
    Violet,
    Pink,
    Red,
    Orange,
    Amber,
    Green,
    Slate,
}

/// <summary>A tag shared by every project in the organization, such as "Backend" or "Bug".</summary>
public sealed class Label : Entity<Guid>, ITenantScoped
{
    public const int MaxNameLength = 40;

    private Label(Guid id, Guid organizationId, string name, LabelColor color)
        : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
        Color = color;
    }

    /// <summary>Used by EF Core.</summary>
    private Label()
    {
        Name = string.Empty;
    }

    public Guid OrganizationId { get; private init; }

    /// <summary>Unique in the organization, ignoring case.</summary>
    public string Name { get; private set; }

    public LabelColor Color { get; private set; }

    public static Result<Label> Create(Guid organizationId, string name, LabelColor color)
    {
        var trimmed = name.Trim();
        return IsValidName(trimmed) ? new Label(Guid.CreateVersion7(), organizationId, trimmed, color) : IssueErrors.InvalidLabelName;
    }

    public Result Update(string name, LabelColor color)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return IssueErrors.InvalidLabelName;
        }

        Name = trimmed;
        Color = color;
        return Result.Success();
    }

    private static bool IsValidName(string name) => name.Length is >= 1 and <= MaxNameLength;
}

/// <summary>A label on an issue.</summary>
public sealed class IssueLabel : ITenantScoped
{
    public IssueLabel(Guid organizationId, Guid issueId, Guid labelId)
    {
        OrganizationId = organizationId;
        IssueId = issueId;
        LabelId = labelId;
    }

    public Guid OrganizationId { get; private init; }

    public Guid IssueId { get; private init; }

    public Guid LabelId { get; private init; }
}
