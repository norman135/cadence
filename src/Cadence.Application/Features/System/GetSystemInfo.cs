using Cadence.Application.Common.Abstractions;
using Mediator;

namespace Cadence.Application.Features.System;

/// <summary>Returns basic information about the running application.</summary>
public sealed record GetSystemInfoQuery : IQuery<SystemInfoResponse>;

/// <summary>Information about the running application.</summary>
/// <param name="Name">The product name.</param>
/// <param name="Version">The semantic version of the running build.</param>
/// <param name="Environment">The hosting environment.</param>
/// <param name="ServerTime">The current server time in UTC.</param>
public sealed record SystemInfoResponse(string Name, string Version, string Environment, DateTimeOffset ServerTime);

public sealed class GetSystemInfoQueryHandler(IApplicationInfo applicationInfo, TimeProvider timeProvider)
    : IQueryHandler<GetSystemInfoQuery, SystemInfoResponse>
{
    public ValueTask<SystemInfoResponse> Handle(GetSystemInfoQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new SystemInfoResponse(
            applicationInfo.Name,
            applicationInfo.Version,
            applicationInfo.Environment,
            timeProvider.GetUtcNow()));
}
