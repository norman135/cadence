namespace Cadence.Application.Common.Abstractions;

/// <summary>Describes the running application. Implemented by the host.</summary>
public interface IApplicationInfo
{
    /// <summary>The product name.</summary>
    string Name { get; }

    /// <summary>The semantic version of the running build, derived from its git tag.</summary>
    string Version { get; }

    /// <summary>The hosting environment, e.g. Development or Production.</summary>
    string Environment { get; }
}
