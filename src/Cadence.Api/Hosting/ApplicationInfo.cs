using System.Reflection;
using Cadence.Application.Common.Abstractions;

namespace Cadence.Api.Hosting;

internal sealed class ApplicationInfo(IHostEnvironment environment) : IApplicationInfo
{
    // MinVer stamps the informational version from the git tag; the SDK appends "+<commit sha>".
    private static readonly string s_version =
        typeof(ApplicationInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    public string Name => "Cadence";

    public string Version => s_version;

    public string Environment => environment.EnvironmentName;
}
