using Cadence.Application.Features.Workflows;

namespace Cadence.Application.Common.Abstractions;

/// <summary>
/// Cached project workflows. Creating issues, changing their status and loading the board all
/// need the workflow, and it rarely changes, so a hit costs no database queries.
/// </summary>
public interface IWorkflowCache
{
    /// <summary>The project's workflow, or <see langword="null"/> when the project doesn't exist in the organization.</summary>
    ValueTask<WorkflowResponse?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>Forgets the project's workflow after it changes.</summary>
    ValueTask InvalidateAsync(Guid projectId, CancellationToken cancellationToken);
}
