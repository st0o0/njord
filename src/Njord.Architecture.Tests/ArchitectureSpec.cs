using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Architecture.Tests;

public sealed class ArchitectureSpec
{
    private static readonly string[] ProjectNames =
    [
        "Njord.Forecast", "Njord.Messages", "Njord.Persistence", "Njord.Core",
        "Njord.Alerts", "Njord.Scores", "Njord.Insights", "Njord.Advisories", "Njord.Grpc",
        "Njord.OpenMeteo", "Njord.Budget", "Njord.Pipeline", "Njord.Polling",
    ];

    private static readonly Assembly[] Assemblies = [.. ProjectNames.Select(Assembly.Load)];

    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        new ArchLoader().LoadAssemblies(Assemblies).Build();

    private static IObjectProvider<IType> In(params string[] names) =>
        Types().That().ResideInAssembly(Assemblies.Single(a => a.GetName().Name == names[0]),
                [.. names.Skip(1).Select(n => Assemblies.Single(a => a.GetName().Name == n))])
            .As(string.Join(" + ", names));

    private static IObjectProvider<IType> AkkaTypes =>
        Types().That().ResideInNamespaceMatching(@"^Akka(\..*)?$").As("Akka types");

    private static IObjectProvider<IType> AllExcept(params string[] allowed) =>
        Types().That().ResideInAssembly(
                Assemblies.Single(a => a.GetName().Name == ProjectNames.First(n => !allowed.Contains(n))),
                [.. ProjectNames.Where(n => !allowed.Contains(n)).Skip(1)
                    .Select(n => Assemblies.Single(a => a.GetName().Name == n))])
            .As("other Njord projects");

    [Fact]
    public void Forecast_depends_on_no_other_Njord_project()
    {
        Types().That().Are(In("Njord.Forecast")).Should()
            .NotDependOnAny(AllExcept("Njord.Forecast")).Check(Architecture);
    }

    [Fact]
    public void Messages_depend_on_nothing_but_Model()
    {
        Types().That().Are(In("Njord.Messages")).Should()
            .NotDependOnAny(AllExcept("Njord.Messages", "Njord.Forecast")).Check(Architecture);
    }

    [Fact]
    public void Persistence_depends_on_no_other_Njord_project()
    {
        Types().That().Are(In("Njord.Persistence")).Should()
            .NotDependOnAny(AllExcept("Njord.Persistence")).Check(Architecture);
    }

    [Fact]
    public void Messages_have_no_Akka_dependency()
    {
        Types().That().Are(In("Njord.Messages")).Should().NotDependOnAny(AkkaTypes).Check(Architecture);
    }

    [Fact]
    public void Persistence_has_no_Akka_dependency()
    {
        Types().That().Are(In("Njord.Persistence")).Should().NotDependOnAny(AkkaTypes).Check(Architecture);
    }

    [Theory]
    [InlineData("Njord.Alerts", "Njord.Scores", "Njord.Insights", "Njord.Advisories")]
    [InlineData("Njord.Scores", "Njord.Alerts", "Njord.Insights", "Njord.Advisories")]
    [InlineData("Njord.Insights", "Njord.Alerts", "Njord.Scores", "Njord.Advisories")]
    [InlineData("Njord.Advisories", "Njord.Alerts", "Njord.Scores", "Njord.Insights")]
    public void Handler_projects_do_not_depend_on_each_other(string handler, params string[] others)
    {
        Types().That().Are(In(handler)).Should().NotDependOnAny(In(others)).Check(Architecture);
    }

    [Theory]
    [InlineData("Njord.Alerts")]
    [InlineData("Njord.Scores")]
    [InlineData("Njord.Insights")]
    [InlineData("Njord.Advisories")]
    public void Handler_projects_do_not_depend_on_adapters(string handler)
    {
        Types().That().Are(In(handler)).Should().NotDependOnAny(In("Njord.Grpc")).Check(Architecture);
    }

    [Fact]
    public void Concrete_classes_are_sealed()
    {
        Classes().That().AreNotAbstract()
            .And().DoNotResideInNamespaceMatching(@"^(Google|Grpc|Njord\.Grpc\.Proto|Microsoft)(\..*)?$")
            .And().DoNotHaveNameContaining("<")
            .And().DoNotHaveFullNameContaining("Njord.Grpc.V2.")
            .And().ResideInAssembly(Assemblies[0], [.. Assemblies.Skip(1)])
            .Should().BeSealed().Check(Architecture);
    }
}
