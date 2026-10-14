using Cadence.Domain.Issues;

namespace Cadence.Domain.UnitTests.Issues;

public sealed class RankTests
{
    [Theory]
    [InlineData(null, null, "a0")]
    [InlineData("a0", null, "a1")]
    [InlineData("a1", null, "a2")]
    [InlineData(null, "a0", "Zz")]
    [InlineData("a1", "a2", "a1V")]
    [InlineData("a0", "a1", "a0V")]
    [InlineData("az", null, "b00")]
    [InlineData(null, "b00", "az")]
    [InlineData("Zz", null, "a0")]
    [InlineData("a0V", "a1", "a0l")]
    [InlineData("a0", "a0V", "a0G")]
    public void Known_keys_match_the_reference_algorithm(string? before, string? after, string expected) =>
        Assert.Equal(expected, Rank.Between(before, after));

    [Fact]
    public void Repeated_inserts_stay_ordered()
    {
        // Always inserting right after the first key is the worst case for key length.
        var low = Rank.Between(null, null);
        var high = Rank.Between(low, null);
        var keys = new List<string> { low, high };
        for (var i = 0; i < 200; i++)
        {
            high = Rank.Between(low, high);
            keys.Add(high);
        }

        var sorted = keys.Order(StringComparer.Ordinal).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal(low, sorted[0]);
        Assert.All(keys, key => Assert.True(Rank.IsValid(key), key));
    }

    [Fact]
    public void Appending_keeps_keys_short()
    {
        string? last = null;
        for (var i = 0; i < 10_000; i++)
        {
            var next = Rank.Between(last, null);
            Assert.True(last is null || string.CompareOrdinal(last, next) < 0);
            last = next;
        }

        Assert.True(last!.Length <= 4, last);
    }

    [Fact]
    public void Positions_are_evenly_spaced_valid_keys_in_order()
    {
        Assert.Equal("d0000", Rank.AtPosition(0));
        Assert.Equal("d0001", Rank.AtPosition(1));
        Assert.Equal("d000z", Rank.AtPosition(61));
        Assert.Equal("d0010", Rank.AtPosition(62));
        Assert.True(string.CompareOrdinal(Rank.AtPosition(61), Rank.AtPosition(62)) < 0);
        Assert.True(Rank.IsValid(Rank.AtPosition(123_456)));

        // A key still fits between two neighbours and after the last one.
        Assert.Equal("d0000V", Rank.Between(Rank.AtPosition(0), Rank.AtPosition(1)));
        Assert.Equal("d0002", Rank.Between(Rank.AtPosition(1), null));
    }

    [Fact]
    public void Keys_must_be_in_order() =>
        Assert.Throws<ArgumentException>(() => Rank.Between("a1", "a0"));
}
