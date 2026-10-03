using Cadence.Api.Hosting;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Comments;
using Cadence.Application.Features.Issues;
using Cadence.Domain.Issues;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

/// <summary>Body of the update-issue request: see <see cref="UpdateIssueCommand"/>.</summary>
public sealed record UpdateIssueRequest(
    string? Title = null,
    string? Description = null,
    IssueType? Type = null,
    IssueStatus? Status = null,
    IssuePriority? Priority = null,
    Guid? AssigneeId = null,
    int? Estimate = null,
    DateOnly? DueDate = null,
    Guid? ParentId = null,
    Guid[]? LabelIds = null,
    IssueField[]? Clear = null);

public sealed record CreateIssueLinkRequest(string TargetKey, IssueLinkType Type);

public sealed record CommentRequest(string Body);

/// <summary>Issues, their links and comments. Project access is checked in the handlers.</summary>
internal static class IssueEndpoints
{
    public static RouteGroupBuilder MapIssueEndpoints(this RouteGroupBuilder api)
    {
        var issues = api.MapGroup($"/organizations/{{{TenantResolutionMiddleware.RouteParameter}:guid}}/issues")
            .WithTags("Issues");

        issues.MapGet("/", ListIssues)
            .WithName(nameof(ListIssues))
            .WithSummary("List issues you can see, filtered and sorted, one page at a time");

        issues.MapPost("/", CreateIssue)
            .WithName(nameof(CreateIssue))
            .WithSummary("Create an issue");

        issues.MapGet("/by-key/{issueKey}", GetIssue)
            .WithName(nameof(GetIssue))
            .WithSummary("Get an issue by its key, such as ATL-142");

        issues.MapPatch("/{issueId:guid}", UpdateIssue)
            .WithName(nameof(UpdateIssue))
            .WithSummary("Change some fields of an issue");

        issues.MapDelete("/{issueId:guid}", DeleteIssue)
            .WithName(nameof(DeleteIssue))
            .WithSummary("Delete an issue");

        issues.MapPost("/{issueId:guid}/links", CreateIssueLink)
            .WithName(nameof(CreateIssueLink))
            .WithSummary("Link an issue to another");

        issues.MapDelete("/{issueId:guid}/links/{linkId:guid}", DeleteIssueLink)
            .WithName(nameof(DeleteIssueLink))
            .WithSummary("Remove a link");

        var comments = issues.MapGroup("/{issueId:guid}/comments").WithTags("Comments");

        comments.MapGet("/", ListComments)
            .WithName(nameof(ListComments))
            .WithSummary("List an issue's comments, oldest first");

        comments.MapPost("/", CreateComment)
            .WithName(nameof(CreateComment))
            .WithSummary("Comment on an issue");

        comments.MapPatch("/{commentId:guid}", EditComment)
            .WithName(nameof(EditComment))
            .WithSummary("Edit your comment (the old text is kept)");

        comments.MapDelete("/{commentId:guid}", DeleteComment)
            .WithName(nameof(DeleteComment))
            .WithSummary("Delete a comment");

        comments.MapGet("/{commentId:guid}/revisions", ListCommentRevisions)
            .WithName(nameof(ListCommentRevisions))
            .WithSummary("List a comment's earlier versions");

        return api;
    }

    private static async Task<Ok<KeysetPage<IssueListItem>>> ListIssues(
        Guid[]? projectId,
        IssueStatus[]? status,
        IssuePriority[]? priority,
        IssueType[]? type,
        Guid[]? assigneeId,
        bool? unassigned,
        Guid[]? labelId,
        Guid? parentId,
        string? q,
        IssueSort? sort,
        string? after,
        int? limit,
        ISender sender,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(
            new ListIssuesQuery(projectId, status, priority, type, assigneeId, unassigned, labelId, parentId, q, sort, after, limit),
            cancellationToken));

    private static async Task<Results<Created<CreatedIssueResponse>, ProblemHttpResult>> CreateIssue(
        CreateIssueCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<IssueDetail>, ProblemHttpResult>> GetIssue(
        string issueKey, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetIssueQuery(issueKey), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateIssue(
        Guid issueId, UpdateIssueRequest request, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(
            new UpdateIssueCommand(
                issueId,
                request.Title,
                request.Description,
                request.Type,
                request.Status,
                request.Priority,
                request.AssigneeId,
                request.Estimate,
                request.DueDate,
                request.ParentId,
                request.LabelIds,
                request.Clear),
            cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteIssue(
        Guid issueId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteIssueCommand(issueId), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> CreateIssueLink(
        Guid issueId, CreateIssueLinkRequest request, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new CreateIssueLinkCommand(issueId, request.TargetKey, request.Type), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteIssueLink(
        Guid issueId, Guid linkId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteIssueLinkCommand(issueId, linkId), cancellationToken)).ToNoContent();

    private static async Task<Results<Ok<KeysetPage<CommentResponse>>, ProblemHttpResult>> ListComments(
        Guid issueId, string? after, int? limit, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListCommentsQuery(issueId, after, limit), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Created<CommentResponse>, ProblemHttpResult>> CreateComment(
        Guid issueId, CommentRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateCommentCommand(issueId, request.Body), cancellationToken);
        return result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> EditComment(
        Guid issueId, Guid commentId, CommentRequest request, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new EditCommentCommand(issueId, commentId, request.Body), cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteComment(
        Guid issueId, Guid commentId, ISender sender, CancellationToken cancellationToken) =>
        (await sender.Send(new DeleteCommentCommand(issueId, commentId), cancellationToken)).ToNoContent();

    private static async Task<Results<Ok<IReadOnlyList<CommentRevisionResponse>>, ProblemHttpResult>> ListCommentRevisions(
        Guid issueId, Guid commentId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListCommentRevisionsQuery(issueId, commentId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
