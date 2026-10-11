using Njord.Compute.Analysis;
using Njord.Core.Actors;

namespace Njord.Architecture.Tests;

public sealed class LayerReferenceSpec
{
    private static string[] NjordReferences<T>()
        => typeof(T).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Njord", System.StringComparison.Ordinal))
            .Order()
            .ToArray();

    [Fact]
    public void Weather_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Weather.Forecast>());
    }

    [Fact]
    public void Sensors_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Sensors.SensorKind>());
    }

    [Fact]
    public void Persistence_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Persistence.BudgetTrackerSnapshotDto>());
    }

    [Fact]
    public void Messages_references_only_Weather_and_Sensors()
    {
        Assert.Empty(NjordReferences<Njord.Messages.Pipeline.WeightedTarget>().Except(["Njord.Weather", "Njord.Sensors"]));
    }

    [Fact]
    public void Compute_references_only_Weather_and_Sensors()
    {
        Assert.Empty(NjordReferences<ConsensusSnapshot>().Except(["Njord.Weather", "Njord.Sensors"]));
    }

    [Fact]
    public void Core_references_only_Weather_Sensors_Messages_Persistence_and_Compute()
    {
        Assert.Empty(NjordReferences<ISchedulerActor>().Except(["Njord.Compute", "Njord.Weather", "Njord.Sensors", "Njord.Messages", "Njord.Persistence"]));
    }

    private static readonly string[] CoreAndBelow = ["Njord.Compute", "Njord.Core", "Njord.Weather", "Njord.Sensors", "Njord.Messages", "Njord.Persistence"];

    [Fact]
    public void Ingest_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Ingest.OpenMeteoClient>().Except(CoreAndBelow));
    }

    [Fact]
    public void SensorHub_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.SensorHub.SensorHubActor>().Except(CoreAndBelow));
    }

    [Fact]
    public void Grpc_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Grpc.WeatherGrpcService>().Except(CoreAndBelow));
    }

    [Fact]
    public void Pipeline_references_only_Core_and_analysis_libraries()
    {
        Assert.Empty(NjordReferences<Njord.Pipeline.PipelineActor>()
            .Except([..CoreAndBelow, "Njord.Alerts", "Njord.Derived", "Njord.Trends", "Njord.Indices"]));
    }

    [Fact]
    public void Mqtt_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Mqtt.MqttStateActor>().Except(CoreAndBelow));
    }

    [Fact]
    public void Alerts_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Alerts.AlertAnalyzer>().Except(CoreAndBelow));
    }

    [Fact]
    public void Derived_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Derived.DerivedAnalyzer>().Except(CoreAndBelow));
    }

    [Fact]
    public void Trends_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Trends.TrendAnalyzer>().Except(CoreAndBelow));
    }

    [Fact]
    public void Indices_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Indices.IndexAnalyzer>().Except(CoreAndBelow));
    }

}
