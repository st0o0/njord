using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Tests.Architecture;

internal static class NjordArchitecture
{
    private static readonly Assembly NjordAssembly = typeof(Njord.Configuration.NjordServiceSetup).Assembly;
    private static readonly Assembly IngestAssembly = typeof(Njord.Ingest.OpenMeteoClient).Assembly;
    private static readonly Assembly GrpcAssembly = typeof(Njord.Grpc.WeatherGrpcService).Assembly;
    private static readonly Assembly SensorsAssembly = typeof(Njord.Sensors.SensorHubActor).Assembly;
    private static readonly Assembly DomainAssembly = typeof(Njord.Domain.Weather.ModelForecast).Assembly;
    private static readonly Assembly PersistenceAssembly = typeof(Njord.Persistence.BudgetTrackerSnapshotDto).Assembly;
    private static readonly Assembly MessagesAssembly = typeof(Njord.Messages.Pipeline.WeightedTarget).Assembly;
    private static readonly Assembly CoreAssembly = typeof(Njord.Actors.ISchedulerActor).Assembly;
    private static readonly Assembly TestsAssembly = typeof(NjordArchitecture).Assembly;

    public static readonly ArchUnitNET.Domain.Architecture Instance =
        new ArchLoader()
            .LoadAssemblies(NjordAssembly, IngestAssembly, SensorsAssembly, GrpcAssembly, DomainAssembly, PersistenceAssembly, MessagesAssembly, CoreAssembly, TestsAssembly)
            .Build();

    public static readonly IObjectProvider<IType> Ingest =
        Types().That().ResideInNamespaceMatching(@"^Njord\.Ingest(\..*)?$").As("Ingest");

    public static readonly IObjectProvider<IType> Domain =
        Types().That().ResideInNamespaceMatching(@"^Njord\.Domain(\..*)?$").As("Domain");

    public static readonly IObjectProvider<IType> EgressSide =
        Types().That().ResideInNamespaceMatching(@"^Njord\.(Egress|Mqtt|Grpc)(\..*)?$").As("Egress side");

    public static IObjectProvider<IType> ProductionTypes =>
        Types().That().ResideInAssembly(NjordAssembly)
            .Or().ResideInAssembly(IngestAssembly)
            .Or().ResideInAssembly(SensorsAssembly)
            .Or().ResideInAssembly(GrpcAssembly)
            .Or().ResideInAssembly(DomainAssembly)
            .Or().ResideInAssembly(PersistenceAssembly)
            .Or().ResideInAssembly(MessagesAssembly)
            .Or().ResideInAssembly(CoreAssembly)
            .As("Njord types");

    public static IObjectProvider<IType> TestTypes =>
        Types().That().ResideInAssembly(TestsAssembly).As("Njord.Tests types");
}
