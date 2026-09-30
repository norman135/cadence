namespace Cadence.Application.Common.ReadModels;

/// <summary>
/// A read-only projection of a user account. The identity store owns accounts; this view lets
/// handlers join user names into their queries without depending on it.
/// </summary>
public sealed class UserSummary
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    /// <summary>The email uppercased by the identity store, which is what its unique index covers.</summary>
    public string NormalizedEmail { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}
