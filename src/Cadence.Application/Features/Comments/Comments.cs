using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Issues;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Comments;

/// <summary>A comment, with how many earlier versions it has.</summary>
public sealed record CommentResponse(
    Guid Id,
    PersonSummary Author,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    int RevisionCount);

public sealed record CommentRevisionResponse(Guid Id, string Body, DateTimeOffset WrittenAt, DateTimeOffset ReplacedAt);

/// <summary>An issue's comments, oldest first, one keyset page at a time.</summary>
public sealed record ListCommentsQuery(Guid IssueId, string? After, int? Limit) : IQuery<Result<KeysetPage<CommentResponse>>>;

public sealed class ListCommentsQueryHandler(ICadenceDbContext db, ProjectGate gate)
    : IQueryHandler<ListCommentsQuery, Result<KeysetPage<CommentResponse>>>
{
    public async ValueTask<Result<KeysetPage<CommentResponse>>> Handle(ListCommentsQuery query, CancellationToken cancellationToken)
    {
        var readable = await ReadableIssue.LoadAsync(db, gate, query.IssueId, cancellationToken);
        if (readable.IsFailure)
        {
            return readable.Error;
        }

        var limit = KeysetPaging.ClampLimit(query.Limit);
        var comments = db.Comments.AsNoTracking().Where(comment => comment.IssueId == query.IssueId);
        if (Guid.TryParse(query.After, out var after))
        {
            comments = comments.Where(comment => comment.Id > after);
        }

        // Comment ids are UUIDv7, so ordering by id is ordering by creation time.
        var rows = await comments
            .OrderBy(comment => comment.Id)
            .Take(limit + 1)
            .Select(comment => new CommentResponse(
                comment.Id,
                new PersonSummary(comment.AuthorId, db.Users.Where(user => user.Id == comment.AuthorId).Select(user => user.DisplayName).First()),
                comment.Body,
                comment.CreatedAt,
                comment.EditedAt,
                db.CommentRevisions.Count(revision => revision.CommentId == comment.Id)))
            .ToListAsync(cancellationToken);

        return KeysetPaging.ToPage(rows, limit, comment => comment.Id.ToString());
    }
}

public sealed record CreateCommentCommand(Guid IssueId, string Body) : ICommand<Result<CommentResponse>>;

public sealed class CreateCommentCommandValidator : AbstractValidator<CreateCommentCommand>
{
    public CreateCommentCommandValidator() =>
        RuleFor(command => command.Body).Must(body => body.Trim().Length is >= 1 and <= Comment.MaxBodyLength)
            .WithMessage($"Comments must be 1 to {Comment.MaxBodyLength} characters.");
}

public sealed class CreateCommentCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateCommentCommand, Result<CommentResponse>>
{
    public async ValueTask<Result<CommentResponse>> Handle(CreateCommentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var created = Comment.Create(loaded.Value.Issue, currentUser.RequiredUserId, command.Body, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        var comment = created.Value;
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        await db.RefreshCommentsAsync(command.IssueId, cancellationToken);

        var author = new PersonSummary(comment.AuthorId, currentUser.DisplayName ?? string.Empty);
        return new CommentResponse(comment.Id, author, comment.Body, comment.CreatedAt, comment.EditedAt, RevisionCount: 0);
    }
}

/// <summary>Edits a comment. Only its author can, and the replaced text is kept as a revision.</summary>
public sealed record EditCommentCommand(Guid IssueId, Guid CommentId, string Body) : ICommand<Result>;

public sealed class EditCommentCommandValidator : AbstractValidator<EditCommentCommand>
{
    public EditCommentCommandValidator() =>
        RuleFor(command => command.Body).Must(body => body.Trim().Length is >= 1 and <= Comment.MaxBodyLength)
            .WithMessage($"Comments must be 1 to {Comment.MaxBodyLength} characters.");
}

public sealed class EditCommentCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<EditCommentCommand, Result>
{
    public async ValueTask<Result> Handle(EditCommentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var comment = await db.Comments
            .SingleOrDefaultAsync(comment => comment.Id == command.CommentId && comment.IssueId == command.IssueId, cancellationToken);
        if (comment is null)
        {
            return IssueErrors.CommentNotFound;
        }

        var edited = comment.Edit(currentUser.RequiredUserId, command.Body, timeProvider.GetUtcNow());
        if (edited.IsFailure)
        {
            return edited.Error;
        }

        if (edited.Value is { } revision)
        {
            db.CommentRevisions.Add(revision);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Deletes a comment and its revisions. Authors may delete their own; project admins any.</summary>
public sealed record DeleteCommentCommand(Guid IssueId, Guid CommentId) : ICommand<Result>;

public sealed class DeleteCommentCommandHandler(ICadenceDbContext db, ProjectGate gate, ICurrentUser currentUser)
    : ICommandHandler<DeleteCommentCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCommentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var comment = await db.Comments
            .SingleOrDefaultAsync(comment => comment.Id == command.CommentId && comment.IssueId == command.IssueId, cancellationToken);
        if (comment is null)
        {
            return IssueErrors.CommentNotFound;
        }

        if (comment.AuthorId != currentUser.RequiredUserId && loaded.Value.Role != ProjectRole.Admin)
        {
            return ProjectErrors.Forbidden;
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
        await db.RefreshCommentsAsync(command.IssueId, cancellationToken);
        return Result.Success();
    }
}

/// <summary>A comment's earlier versions, newest first.</summary>
public sealed record ListCommentRevisionsQuery(Guid IssueId, Guid CommentId) : IQuery<Result<IReadOnlyList<CommentRevisionResponse>>>;

public sealed class ListCommentRevisionsQueryHandler(ICadenceDbContext db, ProjectGate gate)
    : IQueryHandler<ListCommentRevisionsQuery, Result<IReadOnlyList<CommentRevisionResponse>>>
{
    public async ValueTask<Result<IReadOnlyList<CommentRevisionResponse>>> Handle(ListCommentRevisionsQuery query, CancellationToken cancellationToken)
    {
        var readable = await ReadableIssue.LoadAsync(db, gate, query.IssueId, cancellationToken);
        if (readable.IsFailure)
        {
            return readable.Error;
        }

        var revisions = await db.CommentRevisions.AsNoTracking()
            .Where(revision => revision.CommentId == query.CommentId
                && db.Comments.Any(comment => comment.Id == query.CommentId && comment.IssueId == query.IssueId))
            .OrderByDescending(revision => revision.Id)
            .Select(revision => new CommentRevisionResponse(revision.Id, revision.Body, revision.WrittenAt, revision.ReplacedAt))
            .ToListAsync(cancellationToken);

        return revisions;
    }
}
