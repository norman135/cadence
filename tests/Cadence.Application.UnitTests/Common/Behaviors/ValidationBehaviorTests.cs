using Cadence.Application.Common.Behaviors;
using FluentValidation;
using Mediator;

namespace Cadence.Application.UnitTests.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    public sealed record CreateLabel(string Name, string Color) : ICommand<string>;

    private sealed class NameValidator : AbstractValidator<CreateLabel>
    {
        public NameValidator() => RuleFor(command => command.Name).NotEmpty();
    }

    private sealed class ColorValidator : AbstractValidator<CreateLabel>
    {
        public ColorValidator() => RuleFor(command => command.Color).Matches("^#[0-9a-fA-F]{6}$");
    }

    private static MessageHandlerDelegate<CreateLabel, string> Handler(Action? onCalled = null) =>
        (command, _) =>
        {
            onCalled?.Invoke();
            return ValueTask.FromResult($"created {command.Name}");
        };

    [Fact]
    public async Task Calls_the_handler_when_no_validators_are_registered()
    {
        var behavior = new ValidationBehavior<CreateLabel, string>([]);

        var response = await behavior.Handle(new CreateLabel("", ""), Handler(), TestContext.Current.CancellationToken);

        Assert.Equal("created ", response);
    }

    [Fact]
    public async Task Calls_the_handler_when_the_message_is_valid()
    {
        var behavior = new ValidationBehavior<CreateLabel, string>([new NameValidator(), new ColorValidator()]);

        var response = await behavior.Handle(
            new CreateLabel("bug", "#ff0000"),
            Handler(),
            TestContext.Current.CancellationToken);

        Assert.Equal("created bug", response);
    }

    [Fact]
    public async Task Throws_with_the_failures_of_every_validator_and_skips_the_handler()
    {
        var handlerCalled = false;
        var behavior = new ValidationBehavior<CreateLabel, string>([new NameValidator(), new ColorValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(async () => await behavior.Handle(
            new CreateLabel("", "red"),
            Handler(() => handlerCalled = true),
            TestContext.Current.CancellationToken));

        Assert.False(handlerCalled);
        Assert.Equal(
            [nameof(CreateLabel.Name), nameof(CreateLabel.Color)],
            exception.Errors.Select(failure => failure.PropertyName));
    }
}
