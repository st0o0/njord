using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly Assembly ForecastAssembly = typeof(Forecast.AssemblyMarker).Assembly;
    private static readonly Assembly MessagesAssembly = typeof(Messages.AssemblyMarker).Assembly;
    private static readonly Assembly PersistenceAssembly = typeof(Persistence.AssemblyMarker).Assembly;
    private static readonly Assembly CoreAssembly = typeof(Core.AssemblyMarker).Assembly;
    private static readonly Assembly OpenMeteoAssembly = typeof(OpenMeteo.AssemblyMarker).Assembly;
    private static readonly Assembly BudgetAssembly = typeof(Budget.AssemblyMarker).Assembly;
    private static readonly Assembly PollingAssembly = typeof(Polling.AssemblyMarker).Assembly;
    private static readonly Assembly PipelineAssembly = typeof(Pipeline.AssemblyMarker).Assembly;
    private static readonly Assembly FeaturesAssembly = typeof(Features.AssemblyMarker).Assembly;
    private static readonly Assembly GrpcAssembly = typeof(Grpc.AssemblyMarker).Assembly;

    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        new ArchLoader()
            .LoadAssemblies(
                ForecastAssembly,
                MessagesAssembly,
                PersistenceAssembly,
                CoreAssembly,
                OpenMeteoAssembly,
                BudgetAssembly,
                PollingAssembly,
                PipelineAssembly,
                FeaturesAssembly,
                GrpcAssembly)
            .Build();

    private static IObjectProvider<IType> InAssembly(Assembly assembly)
        => Types().That().ResideInAssembly(assembly);

    private static readonly IObjectProvider<IType> ForecastLayer = InAssembly(ForecastAssembly);
    private static readonly IObjectProvider<IType> MessagesLayer = InAssembly(MessagesAssembly);
    private static readonly IObjectProvider<IType> PersistenceLayer = InAssembly(PersistenceAssembly);
    private static readonly IObjectProvider<IType> CoreLayer = InAssembly(CoreAssembly);
    private static readonly IObjectProvider<IType> OpenMeteoLayer = InAssembly(OpenMeteoAssembly);
    private static readonly IObjectProvider<IType> BudgetLayer = InAssembly(BudgetAssembly);
    private static readonly IObjectProvider<IType> PollingLayer = InAssembly(PollingAssembly);
    private static readonly IObjectProvider<IType> PipelineLayer = InAssembly(PipelineAssembly);
    private static readonly IObjectProvider<IType> FeaturesLayer = InAssembly(FeaturesAssembly);
    private static readonly IObjectProvider<IType> GrpcLayer = InAssembly(GrpcAssembly);

    [Fact]
    public void Forecast_should_not_depend_on_any_project()
    {
        Types().That().Are(ForecastLayer)
            .Should().NotDependOnAnyTypesThat().Are(MessagesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PersistenceLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(CoreLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Messages_should_not_depend_on_layers_above_forecast()
    {
        Types().That().Are(MessagesLayer)
            .Should().NotDependOnAnyTypesThat().Are(PersistenceLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(CoreLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Persistence_should_not_depend_on_any_project()
    {
        Types().That().Are(PersistenceLayer)
            .Should().NotDependOnAnyTypesThat().Are(ForecastLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(MessagesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(CoreLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Core_should_not_depend_on_feature_libraries()
    {
        Types().That().Are(CoreLayer)
            .Should().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);
    }

    [Fact]
    public void OpenMeteo_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(OpenMeteoLayer)
            .Should().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Budget_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(BudgetLayer)
            .Should().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Polling_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(PollingLayer)
            .Should().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Pipeline_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(PipelineLayer)
            .Should().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Features_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(FeaturesLayer)
            .Should().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Feature_libraries_should_not_depend_on_adapters()
    {
        Types().That().Are(OpenMeteoLayer)
            .Should().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);

        Types().That().Are(BudgetLayer)
            .Should().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);

        Types().That().Are(PollingLayer)
            .Should().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);

        Types().That().Are(PipelineLayer)
            .Should().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);

        Types().That().Are(FeaturesLayer)
            .Should().NotDependOnAnyTypesThat().Are(GrpcLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Grpc_should_not_depend_on_feature_libraries()
    {
        Types().That().Are(GrpcLayer)
            .Should().NotDependOnAnyTypesThat().Are(OpenMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(BudgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(PipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(FeaturesLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Messages_should_not_depend_on_akka()
    {
        Types().That().Are(MessagesLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka(\..*)?$")
            .Check(Architecture);
    }

    [Fact]
    public void Persistence_should_not_depend_on_akka()
    {
        Types().That().Are(PersistenceLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka(\..*)?$")
            .Check(Architecture);
    }

    [Fact]
    public void All_types_should_be_sealed()
    {
        var allLayers = new[]
        {
            ForecastLayer, MessagesLayer, PersistenceLayer, CoreLayer, OpenMeteoLayer,
            BudgetLayer, PollingLayer, PipelineLayer, FeaturesLayer
        };

        foreach (var layer in allLayers)
        {
            Classes().That().Are(layer)
                .And().AreNotAbstract()
                .And().DoNotHaveNameContaining("<")
                .Should().BeSealed()
                .Check(Architecture);
        }

        Classes().That().Are(GrpcLayer)
            .And().AreNotAbstract()
            .And().DoNotHaveNameContaining("<")
            .And().DoNotResideInNamespace("Njord.Grpc.V2")
            .Should().BeSealed()
            .Check(Architecture);
    }
}
