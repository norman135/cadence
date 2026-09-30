using System.Reflection;

namespace Cadence.Api.Hosting;

/// <summary>
/// The OpenAPI document is generated at build time by starting the app inside the
/// <c>GetDocument.Insider</c> tool (ADR-0014). No real secrets exist there, so required settings get
/// harmless placeholders; nothing is served and no database is contacted.
/// </summary>
internal static class OpenApiDocumentGeneration
{
    public static bool IsRunning =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static void UsePlaceholderSettings(this WebApplicationBuilder builder)
    {
        if (!IsRunning)
        {
            return;
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cadence:PublicUrl"] = "http://localhost",
            ["Cadence:Auth:SigningKey"] = "openapi-document-generation-placeholder-key",
        });
    }
}
