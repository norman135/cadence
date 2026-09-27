using System.Text.Encodings.Web;

namespace Cadence.Infrastructure.Email;

/// <summary>
/// Builds a transactional email with a single call to action, as HTML (inline styles, as email clients
/// require) plus a plain-text alternative. Every value is HTML-encoded.
/// </summary>
internal static class EmailTemplate
{
    public static (string Html, string Text) ActionEmail(
        string heading,
        string greeting,
        string body,
        string actionLabel,
        string actionUrl,
        string footnote)
    {
        var encoder = HtmlEncoder.Default;

        var html = $"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:24px;background:#f6f6f9;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f1f29;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;border:1px solid #e6e6ee;">
                <tr><td style="padding:32px;">
                  <p style="margin:0 0 24px;font-weight:600;color:#4f46e5;">Cadence</p>
                  <h1 style="margin:0 0 16px;font-size:20px;">{encoder.Encode(heading)}</h1>
                  <p style="margin:0 0 12px;line-height:1.5;">{encoder.Encode(greeting)}</p>
                  <p style="margin:0 0 24px;line-height:1.5;">{encoder.Encode(body)}</p>
                  <a href="{encoder.Encode(actionUrl)}" style="display:inline-block;padding:10px 20px;background:#4f46e5;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;">{encoder.Encode(actionLabel)}</a>
                  <p style="margin:24px 0 0;font-size:13px;line-height:1.5;color:#6b6b7b;">{encoder.Encode(footnote)}</p>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        var text = $"""
            {heading}

            {greeting}

            {body}

            {actionLabel}: {actionUrl}

            {footnote}
            """;

        return (html, text);
    }
}
