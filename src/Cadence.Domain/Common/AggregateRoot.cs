namespace Cadence.Domain.Common;

/// <summary>
/// An entity that is the consistency boundary for a cluster of domain objects.
/// Aggregates record domain events as they change; the persistence layer dispatches
/// them in the same transaction that saves the aggregate (transactional outbox).
/// </summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>Used by EF Core when materializing aggregates from the database.</summary>
    protected AggregateRoot()
    {
    }

    /// <summary>Events raised since the aggregate was loaded or last cleared.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Removes all recorded events, typically after they have been persisted to the outbox.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
