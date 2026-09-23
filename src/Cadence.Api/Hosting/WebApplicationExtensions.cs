using Microsoft.Net.Http.Headers;
using Scalar.AspNetCore;

namespace Cadence.Api.Hosting;

internal static class WebApplicationExtensions
{
    /// <summary>Configures the HTTP pipeline shared by every endpoint.</summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        // Unhandled exceptions and bare status codes are returned as RFC 9457 problem details.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().WithDocumentPerVersion();
            app.MapScalarApiReference("/docs");
        }

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var info = app.Services.GetRequiredService<Application.Common.Abstractions.IApplicationInfo>();
            app.Logger.ApplicationStarted(info.Name, info.Version, info.Environment);
        });

        return app;
    }

    /// <summary>
    /// Serves the compiled React app when it is present in wwwroot (container builds).
    /// During development the Vite dev server serves the frontend instead, so this is skipped.
    /// </summary>
    public static WebApplication MapSpa(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath;
        if (string.IsNullOrEmpty(webRoot) || !File.Exists(Path.Combine(webRoot, "index.html")))
        {
            return app;
        }

        // Fingerprinted, pre-compressed (Brotli/gzip) assets with immutable caching and ETags.
        app.MapStaticAssets();

        // Client-side routes resolve to index.html, which must always be revalidated so users
        // pick up new releases immediately.
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = context =>
                context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        });

        return app;
    }
}
