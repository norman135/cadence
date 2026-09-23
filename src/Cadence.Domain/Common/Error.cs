namespace Cadence.Domain.Common;

/// <summary>The category of an <see cref="Error"/>, used by the API layer to choose an HTTP status code.</summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}

/// <summary>
/// An expected, recoverable failure: something the caller can act on, such as a missing issue or a
/// workflow transition that is not allowed. Unexpected failures remain exceptions.
/// </summary>
/// <param name="Code">A stable, machine-readable code, e.g. <c>issues.not_found</c>.</param>
/// <param name="Description">A human-readable explanation.</param>
/// <param name="Type">The error category.</param>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>Represents the absence of an error on a successful <see cref="Result"/>.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
}
