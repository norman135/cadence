using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Cadence.Api.Hosting;

/// <summary>Request rate limits (<c>Cadence:RateLimiting</c>), per client IP or per signed-in user.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "Cadence:RateLimiting";

    /// <summary>Sign-in, registration and password endpoints, per IP: slows down credential guessing.</summary>
    public int AuthPermitsPerMinute { get; set; } = 10;

    /// <summary>Session refreshes, per IP. Higher, because every open tab refreshes periodically.</summary>
    public int RefreshPermitsPerMinute { get; set; } = 60;

    /// <summary>All other API calls, per user (or per IP when anonymous), as a token bucket.</summary>
    public int ApiPermitsPerMinute { get; set; } = 600;
}

internal static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Refresh = "auth-refresh";

    public static IServiceCollection AddCadenceRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context),
                _ => PerMinute(Settings(context).AuthPermitsPerMinute)));

            options.AddPolicy(Refresh, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context),
                _ => PerMinute(Settings(context).RefreshPermitsPerMinute)));

            // Static files and health probes are not limited; API calls are, per user or IP.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api"))
                {
                    return RateLimitPartition.GetNoLimiter("unlimited");
                }

                var permits = Settings(context).ApiPermitsPerMinute;
                var partition = context.User.FindFirst("sub")?.Value ?? ClientIp(context);
                return RateLimitPartition.GetTokenBucketLimiter(partition, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = permits,
                    TokensPerPeriod = permits,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });
    }

    private static RateLimitingOptions Settings(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;

    private static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async ValueTask WriteRejectionAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;

        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Too many requests. Wait a moment and try again.",
            },
        });
    }
}
