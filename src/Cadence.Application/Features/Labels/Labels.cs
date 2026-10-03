using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Labels;

public sealed record LabelResponse(Guid Id, string Name, LabelColor Color);

/// <summary>The organization's labels, by name.</summary>
public sealed record ListLabelsQuery : IQuery<IReadOnlyList<LabelResponse>>;

public sealed class ListLabelsQueryHandler(ICadenceDbContext db) : IQueryHandler<ListLabelsQuery, IReadOnlyList<LabelResponse>>
{
    public async ValueTask<IReadOnlyList<LabelResponse>> Handle(ListLabelsQuery query, CancellationToken cancellationToken) =>
        await db.Labels.AsNoTracking()
            .OrderBy(label => label.Name)
            .Select(label => new LabelResponse(label.Id, label.Name, label.Color))
            .ToListAsync(cancellationToken);
}

public sealed record CreateLabelCommand(string Name, LabelColor Color) : ICommand<Result<LabelResponse>>;

public sealed class CreateLabelCommandValidator : AbstractValidator<CreateLabelCommand>
{
    public CreateLabelCommandValidator()
    {
        RuleFor(command => command.Name).Must(name => name.Trim().Length is >= 1 and <= Label.MaxNameLength)
            .WithMessage($"Name must be 1 to {Label.MaxNameLength} characters.");
        RuleFor(command => command.Color).IsInEnum();
    }
}

public sealed class CreateLabelCommandHandler(ICadenceDbContext db, ITenantContext tenant)
    : ICommandHandler<CreateLabelCommand, Result<LabelResponse>>
{
    public async ValueTask<Result<LabelResponse>> Handle(CreateLabelCommand command, CancellationToken cancellationToken)
    {
        var created = Label.Create(tenant.OrganizationId, command.Name, command.Color);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var label = created.Value;
        if (await LabelNames.IsTakenAsync(db, label.Name, exceptId: null, cancellationToken))
        {
            return IssueErrors.LabelNameTaken;
        }

        db.Labels.Add(label);
        await db.SaveChangesAsync(cancellationToken);
        return new LabelResponse(label.Id, label.Name, label.Color);
    }
}

public sealed record UpdateLabelCommand(Guid LabelId, string Name, LabelColor Color) : ICommand<Result<LabelResponse>>;

public sealed class UpdateLabelCommandValidator : AbstractValidator<UpdateLabelCommand>
{
    public UpdateLabelCommandValidator()
    {
        RuleFor(command => command.Name).Must(name => name.Trim().Length is >= 1 and <= Label.MaxNameLength)
            .WithMessage($"Name must be 1 to {Label.MaxNameLength} characters.");
        RuleFor(command => command.Color).IsInEnum();
    }
}

public sealed class UpdateLabelCommandHandler(ICadenceDbContext db) : ICommandHandler<UpdateLabelCommand, Result<LabelResponse>>
{
    public async ValueTask<Result<LabelResponse>> Handle(UpdateLabelCommand command, CancellationToken cancellationToken)
    {
        var label = await db.Labels.SingleOrDefaultAsync(label => label.Id == command.LabelId, cancellationToken);
        if (label is null)
        {
            return IssueErrors.LabelNotFound;
        }

        if (await LabelNames.IsTakenAsync(db, command.Name.Trim(), label.Id, cancellationToken))
        {
            return IssueErrors.LabelNameTaken;
        }

        var updated = label.Update(command.Name, command.Color);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new LabelResponse(label.Id, label.Name, label.Color);
    }
}

/// <summary>Deletes a label and removes it from every issue (the database cascades).</summary>
public sealed record DeleteLabelCommand(Guid LabelId) : ICommand<Result>;

public sealed class DeleteLabelCommandHandler(ICadenceDbContext db) : ICommandHandler<DeleteLabelCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteLabelCommand command, CancellationToken cancellationToken)
    {
        var deleted = await db.Labels.Where(label => label.Id == command.LabelId).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? IssueErrors.LabelNotFound : Result.Success();
    }
}

internal static class LabelNames
{
    /// <summary>Names are unique ignoring case; the database enforces it too (lower(name) index).</summary>
    public static Task<bool> IsTakenAsync(ICadenceDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLowerInvariant();
#pragma warning disable CA1862 // EF Core translates ToLower() to SQL lower(), matching the lower(name) index.
        return db.Labels.AnyAsync(label => label.Name.ToLower() == lowered && label.Id != exceptId, cancellationToken);
#pragma warning restore CA1862
    }
}
