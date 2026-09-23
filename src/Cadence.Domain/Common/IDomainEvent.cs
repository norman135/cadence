namespace Cadence.Domain.Common;

/// <summary>
/// Marker for something meaningful that happened in the domain, such as an issue changing status.
/// Implementations should be immutable records named in the past tense.
/// </summary>
#pragma warning disable CA1040 // Marker interface is intentional: it keeps the domain free of messaging library types.
public interface IDomainEvent;
#pragma warning restore CA1040
