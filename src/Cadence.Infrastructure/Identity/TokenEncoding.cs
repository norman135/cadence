using System.Buffers.Text;
using System.Text;

namespace Cadence.Infrastructure.Identity;

/// <summary>
/// Identity's tokens contain '+', '/' and '='; they are base64url-encoded so they survive being put in
/// email links and query strings unchanged.
/// </summary>
internal static class TokenEncoding
{
    public static string Encode(string token) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));

    public static bool TryDecode(string encoded, out string token)
    {
        try
        {
            token = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
            return true;
        }
        catch (FormatException)
        {
            token = string.Empty;
            return false;
        }
    }
}
