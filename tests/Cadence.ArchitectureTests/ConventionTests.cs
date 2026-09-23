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
    public void Messages_are_immutable_records_named_after_their_intent()
    {
        var result = s_application
            .That().ImplementInterface(typeof(IQuery<>))
            .Should().BeImmutable()
            .And().HaveNameEndingWith("Query")
            .GetResult();

        Assert.True(result.IsSuccessful, "Queries must be immutable and end with 'Query': " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
