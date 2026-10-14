using System.Globalization;

namespace Cadence.Domain.Issues;

/// <summary>
/// Fractional-index keys that order issues in a project (board columns and, later, the backlog).
/// A key always fits between two others, so moving an issue rewrites only that issue's key
/// (ADR-0010).
/// </summary>
/// <remarks>
/// Keys compare by ordinal (byte) order: the database column uses the "C" collation, and the web
/// client compares them as plain strings. A key is an integer part, whose first character encodes
/// its length (a–z for 2–27 characters, A–Z for negative integers), followed by an optional
/// fraction in base 62 that never ends in "0". Appending after the last key increments the
/// integer, so keys stay short; inserting between two keys extends the fraction. The web client
/// implements the same algorithm in <c>web/src/shared/lib/rank.ts</c>.
/// </remarks>
public static class Rank
{
    public const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>Longest key the API accepts. Keys this long are rebalanced long before.</summary>
    public const int MaxLength = 64;

    /// <summary>Keys longer than this are rebalanced in the background.</summary>
    public const int RebalanceLength = 12;

    private const char Zero = '0';
    private static readonly string s_smallestInteger = "A" + new string(Zero, 26);

    /// <summary>A key between <paramref name="before"/> and <paramref name="after"/>; either may be open.</summary>
    /// <exception cref="ArgumentException">A key is invalid, or <paramref name="before"/> is not below <paramref name="after"/>.</exception>
    public static string Between(string? before, string? after)
    {
        if (before is not null)
        {
            Validate(before);
        }

        if (after is not null)
        {
            Validate(after);
        }

        if (before is not null && after is not null && string.CompareOrdinal(before, after) >= 0)
        {
            throw new ArgumentException($"{before} is not below {after}.", nameof(before));
        }

        if (before is null)
        {
            if (after is null)
            {
                return "a" + Zero;
            }

            var afterInteger = IntegerPart(after);
            var afterFraction = after[afterInteger.Length..];
            if (afterInteger == s_smallestInteger)
            {
                return afterInteger + Midpoint(string.Empty, afterFraction);
            }

            if (string.CompareOrdinal(afterInteger, after) < 0)
            {
                return afterInteger;
            }

            return Decrement(afterInteger) ?? throw new ArgumentException("No key fits below the smallest key.", nameof(after));
        }

        var beforeInteger = IntegerPart(before);
        var beforeFraction = before[beforeInteger.Length..];

        if (after is null)
        {
            return Increment(beforeInteger) ?? beforeInteger + Midpoint(beforeFraction, null);
        }

        var afterInt = IntegerPart(after);
        if (beforeInteger == afterInt)
        {
            return beforeInteger + Midpoint(beforeFraction, after[afterInt.Length..]);
        }

        var next = Increment(beforeInteger) ?? throw new ArgumentException("No key fits above the largest key.", nameof(before));
        return string.CompareOrdinal(next, after) < 0 ? next : beforeInteger + Midpoint(beforeFraction, null);
    }

    /// <summary>
    /// The key for the <paramref name="position"/>-th issue of an evenly spaced sequence: "d" and four
    /// base-62 digits. Rebalancing and the migration use the same scheme in SQL.
    /// </summary>
    public static string AtPosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Span<char> key = ['d', Zero, Zero, Zero, Zero];
        var remaining = position;
        for (var index = 4; index >= 1; index--)
        {
            key[index] = Digits[remaining % Digits.Length];
            remaining /= Digits.Length;
        }

        if (remaining > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Too many positions for a four-digit key.");
        }

        return new string(key);
    }

    /// <summary>Whether <paramref name="key"/> is a well-formed key the API can store.</summary>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxLength || key.Any(character => !Digits.Contains(character, StringComparison.Ordinal)))
        {
            return false;
        }

        try
        {
            Validate(key);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Validate(string key)
    {
        if (key == s_smallestInteger)
        {
            throw new ArgumentException($"{key} is reserved.", nameof(key));
        }

        var integer = IntegerPart(key);
        if (key.Length > integer.Length && key[^1] == Zero)
        {
            throw new ArgumentException($"{key} has a trailing zero.", nameof(key));
        }
    }

    private static string IntegerPart(string key)
    {
        var length = IntegerLength(key[0]);
        if (length > key.Length)
        {
            throw new ArgumentException($"{key} is shorter than its integer part.", nameof(key));
        }

        return key[..length];
    }

    private static int IntegerLength(char head) => head switch
    {
        >= 'a' and <= 'z' => head - 'a' + 2,
        >= 'A' and <= 'Z' => 'Z' - head + 2,
        _ => throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Invalid key head '{head}'.")),
    };

    /// <summary>A fraction strictly between <paramref name="low"/> and <paramref name="high"/> (open when null).</summary>
    private static string Midpoint(string low, string? high)
    {
        if (high is not null)
        {
            // Skip the common prefix; the low side is padded with zeros.
            var shared = 0;
            while (shared < high.Length && (shared < low.Length ? low[shared] : Zero) == high[shared])
            {
                shared++;
            }

            if (shared > 0)
            {
                return high[..shared] + Midpoint(low.Length > shared ? low[shared..] : string.Empty, high[shared..]);
            }
        }

        var lowDigit = low.Length > 0 ? Digits.IndexOf(low[0], StringComparison.Ordinal) : 0;
        var highDigit = high is not null ? Digits.IndexOf(high[0], StringComparison.Ordinal) : Digits.Length;
        if (highDigit - lowDigit > 1)
        {
            return Digits[(int)Math.Round(0.5 * (lowDigit + highDigit), MidpointRounding.AwayFromZero)].ToString();
        }

        // Adjacent digits: take the high digit alone if there is room after it, otherwise keep the
        // low digit and look for room further right.
        if (high is { Length: > 1 })
        {
            return high[..1];
        }

        return Digits[lowDigit] + Midpoint(low.Length > 1 ? low[1..] : string.Empty, null);
    }

    private static string? Increment(string integer)
    {
        var head = integer[0];
        var digits = integer[1..].ToCharArray();
        var carry = true;
        for (var index = digits.Length - 1; carry && index >= 0; index--)
        {
            var digit = Digits.IndexOf(digits[index], StringComparison.Ordinal) + 1;
            if (digit == Digits.Length)
            {
                digits[index] = Zero;
            }
            else
            {
                digits[index] = Digits[digit];
                carry = false;
            }
        }

        if (!carry)
        {
            return head + new string(digits);
        }

        if (head == 'Z')
        {
            return "a" + Zero;
        }

        if (head == 'z')
        {
            return null;
        }

        var nextHead = (char)(head + 1);
        var result = nextHead > 'a' ? new string(digits) + Zero : new string(digits)[..^1];
        return nextHead + result;
    }

    private static string? Decrement(string integer)
    {
        var head = integer[0];
        var digits = integer[1..].ToCharArray();
        var borrow = true;
        for (var index = digits.Length - 1; borrow && index >= 0; index--)
        {
            var digit = Digits.IndexOf(digits[index], StringComparison.Ordinal) - 1;
            if (digit == -1)
            {
                digits[index] = Digits[^1];
            }
            else
            {
                digits[index] = Digits[digit];
                borrow = false;
            }
        }

        if (!borrow)
        {
            return head + new string(digits);
        }

        if (head == 'a')
        {
            return "Z" + Digits[^1];
        }

        if (head == 'A')
        {
            return null;
        }

        var previousHead = (char)(head - 1);
        var result = previousHead < 'Z' ? new string(digits) + Digits[^1] : new string(digits)[..^1];
        return previousHead + result;
    }
}
