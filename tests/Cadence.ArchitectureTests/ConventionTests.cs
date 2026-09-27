using Mediator;
using NetArchTest.Rules;

namespace Cadence.ArchitectureTests;

/// <summary>Conventions that keep the codebase consistent as it grows.</summary>
public sealed class ConventionTests
{
    private static readonly Types s_application = Types.InAssembly(typeof(Application.DependencyInjection).Assembly);

    [Fact]
    public void Convention_rules_select_real_types()
    {
        // Guards against the rules below passing vacuously because a selector matched nothing.
        Assert.NotEmpty(s_application.That().ImplementInterface(typeof(IQueryHandler<,>)).GetTypes());
        Assert.NotEmpty(s_application.That().ImplementInterface(typeof(IQuery<>)).GetTypes());
    }

    [Fact]
    public void Handlers_are_sealed_and_live_in_feature_folders()
    {
        var result = s_application
            .That().ImplementInterface(typeof(IQueryHandler<,>))
            .Or().ImplementInterface(typeof(ICommandHandler<,>))
            .Or().ImplementInterface(typeof(ICommandHandler<>))
            .Should().BeSealed()
            .And().ResideInNamespaceStartingWith("Cadence.Application.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, "Handlers must be sealed and live under Features: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Queries_are_named_after_their_intent()
    {
        var result = s_application.That().ImplementInterface(typeof(IQuery<>)).Should().HaveNameEndingWith("Query").GetResult();

        Assert.True(result.IsSuccessful, "Queries must end with 'Query': " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Commands_are_named_after_their_intent()
    {
        var result = s_application
            .That().ImplementInterface(typeof(ICommand<>))
            .Should().HaveNameEndingWith("Command")
            .GetResult();

        Assert.True(result.IsSuccessful, "Commands must end with 'Command': " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Messages_are_immutable()
    {
        // Positional records use init-only setters, which are immutable after construction; any other
        // setter makes a message mutable. (NetArchTest's BeImmutable treats init as mutable.)
        var mutable = s_application
            .That().ImplementInterface(typeof(IQuery<>)).Or().ImplementInterface(typeof(ICommand<>))
            .GetTypes()
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => pair.property.SetMethod is { } setter
                && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}")
            .ToList();

        Assert.True(mutable.Count == 0, "Messages must be immutable: " + string.Join(", ", mutable));
    }
}
