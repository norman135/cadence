using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Cadence.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the message before its handler.
/// All failures are collected and thrown together as one <see cref="ValidationException"/>,
/// which the API turns into a 400 response with per-field errors.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    // DI already resolves IEnumerable<T> as an array; avoid a copy on every request.
    private readonly IValidator<TMessage>[] _validators = validators as IValidator<TMessage>[] ?? [.. validators];

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Fast path: most queries have no validator, so skip all validation work.
        if (_validators.Length == 0)
        {
            return await next(message, cancellationToken);
        }

        List<ValidationFailure>? failures = null;

        foreach (var validator in _validators)
        {
            // Each validator gets its own context: a shared context accumulates failures across
            // validators, which would report earlier failures more than once.
            var result = await validator.ValidateAsync(message, cancellationToken);
            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is not null)
        {
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
