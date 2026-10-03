using Cadence.Api.Hosting;
using Cadence.Application.Features.Labels;
using Cadence.Application.Features.Projects;
using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

/// <summary>Body of the update-project request. Fields left out are unchanged.</summary>
public sealed record UpdateProjectRequest(string? Name, string? Description);

public sealed record AddProjectMemberRequest(Guid UserId, ProjectRole Role);

public sealed record ChangeProjectMemberRoleRequest(ProjectRole Role);

public sealed record UpdateLabelRequest(string Name, LabelColor Color);

/// <summary>
/// Projects, their members, and the organization's labels. Project access is checked in the handlers:
/// people without access get 404, so a project's existence isn't revealed.
/// </summary>
internal static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder api)
    {
        var organization = api.MapGroup($"/organizations/{{{TenantResolutionMiddleware.RouteParameter}:guid}}");

        var projects = organization.MapGroup("/projects").WithTags("Projects");

        projects.MapGet("/", ListProjects)
            .WithName(nameof(ListProjects))
            .WithSummary("List the projects you can see");

        projects.MapPost("/", CreateProject)
            .WithName(nameof(CreateProject))
            .WithSummary("Create a project with you as its admin")
            .RequirePermission(Permissions.ProjectsCreate);

        projects.MapGet("/{projectId:guid}", GetProject)
            .WithName(nameof(GetProject))
            .WithSummary("Get a project");

        projects.MapPatch("/{projectId:guid}", UpdateProject)
            .WithName(nameof(UpdateProject))
            .WithSummary("Rename or re-describe a project");

        projects.MapPost("/{projectId:guid}/archive", ArchiveProject)
            .WithName(nameof(ArchiveProject))
            .WithSummary("Archive a project");

        projects.MapPost("/{projectId:guid}/restore", RestoreProject)
            .WithName(nameof(RestoreProject))
            .WithSummary("Restore an archived project");

        var members = projects.MapGroup("/{projectId:guid}/members").WithTags("Projects");

        members.MapGet("/", ListProjectMembers)
            .WithName(nameof(ListProjectMembers))
            .WithSummary("List everyone with access to a project");

        members.MapPost("/", AddProjectMember)
            .WithName(nameof(AddProjectMember))
            .WithSummary("Give an organization member access to a project");

        members.MapPatch("/{userId:guid}", ChangeProjectMemberRole)
            .WithName(nameof(ChangeProjectMemberRole))
            .WithSummary("Change a project member's role");

        members.MapDelete("/{userId:guid}", RemoveProjectMember)
            .WithName(nameof(RemoveProjectMember))
            .WithSummary("Remove a project member, or leave the project");

        var labels = organization.MapGroup("/labels").WithTags("Labels");

        labels.MapGet("/", ListLabels)
            .WithName(nameof(ListLabels))
            .WithSummary("List the organization's labels");

        labels.MapPost("/", CreateLabel)
            .WithName(nameof(CreateLabel))
            .WithSummary("Create a label")
            .RequirePermission(Permissions.LabelsManage);

        labels.MapPatch("/{labelId:guid}", UpdateLabel)
            .WithName(nameof(UpdateLabel))
            .WithSummary("Rename or recolor a label")
            .RequirePermission(Permissions.LabelsManage);

        labels.MapDelete("/{labelId:guid}", DeleteLabel)
            .WithName(nameof(DeleteLabel))
            .WithSummary("Delete a label and remove it from every issue")
            .RequirePermission(Permissions.LabelsManage);

        return api;
    }

    private static async Task<Ok<IReadOnlyList<ProjectResponse>>> ListProjects(
        bool? includeArchived, ISender sender, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new ListProjectsQuery(includeArchived ?? false), cancellationToken));

    private static async Task<Results<Created<ProjectResponse>, ProblemHttpResult>> CreateProject(
        CreateProjectCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> GetProject(
        Guid projectId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProjectQuery(projectId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> UpdateProject(
        Guid projectId, UpdateProjectRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProjectCommand(projectId, request.Name, request.Description), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ArchiveProject(
        Guid projectId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new ArchiveProjectCommand(projectId), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> RestoreProject(
        Guid projectId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new RestoreProjectCommand(projectId), cancellationToken)).ToNoContent();

    private static async Task<Results<Ok<IReadOnlyList<ProjectMemberResponse>>, ProblemHttpResult>> ListProjectMembers(
        Guid projectId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListProjectMembersQuery(projectId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AddProjectMember(
        Guid projectId, AddProjectMemberRequest request, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new AddProjectMemberCommand(projectId, request.UserId, request.Role), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeProjectMemberRole(
        Guid projectId, Guid userId, ChangeProjectMemberRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new ChangeProjectMemberRoleCommand(projectId, userId, request.Role), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveProjectMember(
        Guid projectId, Guid userId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new RemoveProjectMemberCommand(projectId, userId), cancellationToken)).ToNoContent();

    private static async Task<Ok<IReadOnlyList<LabelResponse>>> ListLabels(ISender sender, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new ListLabelsQuery(), cancellationToken));

    private static async Task<Results<Created<LabelResponse>, ProblemHttpResult>> CreateLabel(
        CreateLabelCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<LabelResponse>, ProblemHttpResult>> UpdateLabel(
        Guid labelId, UpdateLabelRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateLabelCommand(labelId, request.Name, request.Color), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLabel(
        Guid labelId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteLabelCommand(labelId), cancellationToken)).ToNoContent();
}

internal static class ResultHttpExtensions
{
    public static Results<NoContent, ProblemHttpResult> ToNoContent(this Domain.Common.Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
}
