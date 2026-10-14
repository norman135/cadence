using Cadence.Api.Hosting;
using Cadence.Application.Features.Board;
using Cadence.Application.Features.Workflows;
using Cadence.Domain.Issues;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

/// <summary>Body of the update-workflow request: the whole workflow as the editor shows it.</summary>
public sealed record UpdateWorkflowRequest(
    WorkflowStatusInput[] Statuses,
    bool RestrictTransitions,
    WorkflowTransitionInput[] Transitions,
    WorkflowReplacementInput[]? Replacements = null);

/// <summary>Body of a board move: the card, where it was as the client saw it, and where it goes.</summary>
public sealed record MoveCardRequest(Guid IssueId, Guid FromStatusId, Guid ToStatusId, string Rank, uint Version, Guid? AssigneeId);

/// <summary>A project's workflow and its board. Project access is checked in the handlers.</summary>
internal static class BoardEndpoints
{
    public static RouteGroupBuilder MapBoardEndpoints(this RouteGroupBuilder api)
    {
        var project = api.MapGroup($"/organizations/{{{TenantResolutionMiddleware.RouteParameter}:guid}}/projects/{{projectId:guid}}");

        var workflow = project.MapGroup("/workflow").WithTags("Workflows");

        workflow.MapGet("/", GetWorkflow)
            .WithName(nameof(GetWorkflow))
            .WithSummary("Get a project's workflow");

        workflow.MapPut("/", UpdateWorkflow)
            .WithName(nameof(UpdateWorkflow))
            .WithSummary("Replace a project's statuses and transitions");

        var board = project.MapGroup("/board").WithTags("Board");

        board.MapGet("/", GetBoard)
            .WithName(nameof(GetBoard))
            .WithSummary("Get a project's board: columns and their first cards");

        board.MapPost("/moves", MoveCard)
            .WithName(nameof(MoveCard))
            .WithSummary("Move a card to a status and position");

        return api;
    }

    private static async Task<Results<Ok<WorkflowResponse>, ProblemHttpResult>> GetWorkflow(
        Guid projectId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWorkflowQuery(projectId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<WorkflowResponse>, ProblemHttpResult>> UpdateWorkflow(
        Guid projectId, UpdateWorkflowRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateWorkflowCommand(projectId, request.Statuses, request.RestrictTransitions, request.Transitions, request.Replacements),
            cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> GetBoard(
        Guid projectId,
        Guid[]? assigneeId,
        bool? unassigned,
        IssueType[]? type,
        IssuePriority[]? priority,
        Guid[]? labelId,
        Guid? epicId,
        string? q,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBoardQuery(projectId, assigneeId, unassigned, type, priority, labelId, epicId, q), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<MovedCardResponse>, ProblemHttpResult>> MoveCard(
        Guid projectId, MoveCardRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new MoveCardCommand(projectId, request.IssueId, request.FromStatusId, request.ToStatusId, request.Rank, request.Version, request.AssigneeId),
            cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
