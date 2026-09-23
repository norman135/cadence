using System.Reflection;
using NetArchTest.Rules;

namespace Cadence.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule: dependencies point inwards only.
///   Api → Infrastructure → Application → Domain
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly s_domain = typeof(Domain.Common.Entity<>).Assembly;
    private static readonly Assembly s_application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly s_infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;

    public static TheoryData<string> DomainForbiddenDependencies =>
    [
        "Cadence.Application",
        "Cadence.Infrastructure",
        "Cadence.Api",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Mediator",
        "FluentValidation",
    ];

    public static TheoryData<string> ApplicationForbiddenDependencies =>
    [
        "Cadence.Infrastructure",
        "Cadence.Api",
        "Microsoft.AspNetCore",
        "Npgsql",
    ];

    [Theory]
    [MemberData(nameof(DomainForbiddenDependencies))]
    public void Domain_depends_on_nothing_outside_itself(string forbidden) =>
        AssertNoDependency(s_domain, forbidden);

    [Theory]
    [MemberData(nameof(ApplicationForbiddenDependencies))]
    public void Application_does_not_depend_on_outer_layers_or_frameworks(string forbidden) =>
        AssertNoDependency(s_application, forbidden);

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api() =>
        AssertNoDependency(s_infrastructure, "Cadence.Api");

    private static void AssertNoDependency(Assembly assembly, string forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn(forbidden).GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on {forbidden}, but these types do: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
