using Cadence.Application.Common.Abstractions;

namespace Cadence.Api.Hosting;

/// <summary><see cref="ICurrentUser"/> read from the validated access token's <c>sub</c> claim.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value, out var userId)
            ? userId
            : null;
}
