using Cadence.Domain.Common;

namespace Cadence.Domain.UnitTests.Common;

public sealed class AggregateRootTests
{
    private sealed record Renamed(string NewName) : IDomainEvent;

    private sealed class Board(Guid id) : AggregateRoot<Guid>(id)
    {
        public string Name { get; private set; } = string.Empty;

        public void Rename(string name)
        {
            Name = name;
            Raise(new Renamed(name));
        }

        public void RaiseNull() => Raise(null!);
    }

    [Fact]
    public void A_new_aggregate_has_no_domain_events()
    {
        var board = new Board(Guid.CreateVersion7());

        Assert.Empty(board.DomainEvents);
    }

    [Fact]
    public void Raised_events_are_recorded_in_order()
    {
        var board = new Board(Guid.CreateVersion7());

        board.Rename("Sprint 1");
        board.Rename("Sprint 2");

        Assert.Equal([new Renamed("Sprint 1"), new Renamed("Sprint 2")], board.DomainEvents);
    }

    [Fact]
    public void Clearing_removes_all_recorded_events()
    {
        var board = new Board(Guid.CreateVersion7());
        board.Rename("Sprint 1");

        board.ClearDomainEvents();

        Assert.Empty(board.DomainEvents);
    }

    [Fact]
    public void Raising_a_null_event_throws()
    {
        var board = new Board(Guid.CreateVersion7());

        Assert.Throws<ArgumentNullException>(board.RaiseNull);
    }
}
