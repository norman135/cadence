namespace Cadence.Application.Common.Abstractions;

/// <summary>The authenticated user making the current request. Implemented by the host.</summary>
public interface ICurrentUser
{
    /// <summary>The user's id, or <see langword="null"/> for anonymous requests.</summary>
    Guid? UserId { get; }

    /// <summary>The display name from the access token (may lag a rename by one token lifetime).</summary>
    string? DisplayName { get; }

    /// <summary>The user's id.</summary>
    /// <exception cref="InvalidOperationException">The request is not authenticated.</exception>
    Guid RequiredUserId => UserId ?? throw new InvalidOperationException("The current request is not authenticated.");
}
