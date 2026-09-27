using Cadence.Domain.Common;

namespace Cadence.Domain.UnitTests.Common;

public sealed class EntityTests
{
    private sealed class Widget(Guid id) : Entity<Guid>(id);

    private sealed class Gadget(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Entities_of_the_same_type_with_the_same_id_are_equal()
    {
        var id = Guid.CreateVersion7();

        var first = new Widget(id);
        var second = new Widget(id);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var first = new Widget(Guid.CreateVersion7());
        var second = new Widget(Guid.CreateVersion7());

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void Entities_of_different_types_are_not_equal_even_with_the_same_id()
    {
        var id = Guid.CreateVersion7();

        Assert.False(new Widget(id).Equals(new Gadget(id)));
    }

    [Fact]
    public void An_entity_is_not_equal_to_null()
    {
        var widget = new Widget(Guid.CreateVersion7());

        Assert.False(widget.Equals(null));
        Assert.False(widget == null);
        Assert.True(widget != null);
    }
}
