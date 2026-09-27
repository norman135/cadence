namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>Asserts how many database round-trips an operation makes.</summary>
public static class QueryBudget
{
    /// <summary>Runs <paramref name="action"/> and fails if it executes more than <paramref name="maxQueries"/> commands.</summary>
    public static async Task<T> AssertAtMostAsync<T>(this QueryCounter counter, int maxQueries, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentNullException.ThrowIfNull(action);

        counter.Reset();
        var result = await action();

        Assert.True(
            counter.Count <= maxQueries,
            $"Query budget exceeded: expected at most {maxQueries} database command(s) but {counter.Count} were executed.");

        return result;
    }
}
