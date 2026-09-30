namespace Cadence.Application.Common.Paging;

/// <summary>
/// One page of results with keyset pagination: the next page starts after <see cref="NextCursor"/>,
/// so reading page 50 costs the same as page 1 (no OFFSET scans) and inserts don't shift pages.
/// </summary>
/// <param name="Items">The items on this page.</param>
/// <param name="NextCursor">Pass as <c>after</c> to get the next page; <see langword="null"/> on the last page.</param>
public sealed record KeysetPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public static class KeysetPaging
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    /// <summary>
    /// Builds a page from up to <paramref name="limit"/> + 1 fetched items: the extra item only signals
    /// that another page exists.
    /// </summary>
    public static KeysetPage<T> ToPage<T>(List<T> fetched, int limit, Func<T, string> cursorOf)
    {
        ArgumentNullException.ThrowIfNull(fetched);
        ArgumentNullException.ThrowIfNull(cursorOf);

        if (fetched.Count <= limit)
        {
            return new KeysetPage<T>(fetched, NextCursor: null);
        }

        fetched.RemoveAt(fetched.Count - 1);
        return new KeysetPage<T>(fetched, cursorOf(fetched[^1]));
    }
}
