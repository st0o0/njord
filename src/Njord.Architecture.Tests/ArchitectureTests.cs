using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly Assembly _messagesAssembly = typeof(Messages.AssemblyMarker).Assembly;
    private static readonly Assembly _persistenceAssembly = typeof(Persistence.AssemblyMarker).Assembly;
    private static readonly Assembly _coreAssembly = typeof(Core.AssemblyMarker).Assembly;
    private static readonly Assembly _openMeteoAssembly = typeof(OpenMeteo.AssemblyMarker).Assembly;
    private static readonly Assembly _budgetAssembly = typeof(Budget.AssemblyMarker).Assembly;
    private static readonly Assembly _pollingAssembly = typeof(Polling.AssemblyMarker).Assembly;
    private static readonly Assembly _pipelineAssembly = typeof(Pipeline.AssemblyMarker).Assembly;
    private static readonly Assembly _featuresAssembly = typeof(Features.AssemblyMarker).Assembly;
    private static readonly Assembly _grpcAssembly = typeof(Grpc.AssemblyMarker).Assembly;

    private static readonly ArchUnitNET.Domain.Architecture _architecture =
        new ArchLoader()
            .LoadAssemblies(
                _messagesAssembly,
                _persistenceAssembly,
                _coreAssembly,
                _openMeteoAssembly,
                _budgetAssembly,
                _pollingAssembly,
                _pipelineAssembly,
                _featuresAssembly,
                _grpcAssembly)
            .Build();

    private static IObjectProvider<IType> InAssembly(Assembly assembly)
        => Types().That().ResideInAssembly(assembly)
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage");

    private static readonly IObjectProvider<IType> _messagesLayer = InAssembly(_messagesAssembly);
    private static readonly IObjectProvider<IType> _persistenceLayer = InAssembly(_persistenceAssembly);
    private static readonly IObjectProvider<IType> _coreLayer = InAssembly(_coreAssembly);
    private static readonly IObjectProvider<IType> _openMeteoLayer = InAssembly(_openMeteoAssembly);
    private static readonly IObjectProvider<IType> _budgetLayer = InAssembly(_budgetAssembly);
    private static readonly IObjectProvider<IType> _pollingLayer = InAssembly(_pollingAssembly);
    private static readonly IObjectProvider<IType> _pipelineLayer = InAssembly(_pipelineAssembly);
    private static readonly IObjectProvider<IType> _featuresLayer = InAssembly(_featuresAssembly);
    private static readonly IObjectProvider<IType> _grpcLayer = InAssembly(_grpcAssembly);

    [Fact]
    public void Messages_should_not_depend_on_layers_above_core()
    {
        Types().That().Are(_messagesLayer)
            .Should().NotDependOnAnyTypesThat().Are(_persistenceLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Persistence_should_not_depend_on_any_project()
    {
        Types().That().Are(_persistenceLayer)
            .Should().NotDependOnAnyTypesThat().Are(_messagesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_coreLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Core_should_not_depend_on_messages_or_feature_libraries()
    {
        Types().That().Are(_coreLayer)
            .Should().NotDependOnAnyTypesThat().Are(_messagesLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);
    }

    [Fact]
    public void OpenMeteo_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(_openMeteoLayer)
            .Should().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Budget_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(_budgetLayer)
            .Should().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Polling_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(_pollingLayer)
            .Should().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Pipeline_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(_pipelineLayer)
            .Should().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Features_should_not_depend_on_other_feature_libraries()
    {
        Types().That().Are(_featuresLayer)
            .Should().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Feature_libraries_should_not_depend_on_adapters()
    {
        Types().That().Are(_openMeteoLayer)
            .Should().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);

        Types().That().Are(_budgetLayer)
            .Should().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);

        Types().That().Are(_pollingLayer)
            .Should().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);

        Types().That().Are(_pipelineLayer)
            .Should().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);

        Types().That().Are(_featuresLayer)
            .Should().NotDependOnAnyTypesThat().Are(_grpcLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Grpc_should_not_depend_on_feature_libraries()
    {
        Types().That().Are(_grpcLayer)
            .Should().NotDependOnAnyTypesThat().Are(_openMeteoLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_budgetLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pollingLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_pipelineLayer)
            .AndShould().NotDependOnAnyTypesThat().Are(_featuresLayer)
            .Check(_architecture);
    }

    [Fact]
    public void Messages_should_not_depend_on_akka_beyond_streams()
    {
        Types().That().Are(_messagesLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka\.Actor(\..*)?$")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka\.Cluster(\..*)?$")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka\.Persistence(\..*)?$")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka\.Hosting(\..*)?$")
            .Check(_architecture);
    }

    [Fact]
    public void Persistence_should_not_depend_on_akka()
    {
        Types().That().Are(_persistenceLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Akka(\..*)?$")
            .Check(_architecture);
    }

    [Fact]
    public void All_types_should_be_sealed()
    {
        var allLayers = new[]
        {
            _messagesLayer, _persistenceLayer, _coreLayer, _openMeteoLayer,
            _budgetLayer, _pollingLayer, _pipelineLayer, _featuresLayer
        };

        foreach (var layer in allLayers)
        {
            Classes().That().Are(layer)
                .And().AreNotAbstract()
                .And().DoNotHaveNameContaining("<")
                .Should().BeSealed()
                .Check(_architecture);
        }

        Classes().That().Are(_grpcLayer)
            .And().AreNotAbstract()
            .And().DoNotHaveNameContaining("<")
            .And().DoNotResideInNamespace("Njord.Grpc.V2")
            .Should().BeSealed()
            .Check(_architecture);
    }
}
