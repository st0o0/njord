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
    public void Domain_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Domain.Weather.ModelForecast>());
    }

    [Fact]
    public void Persistence_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Persistence.BudgetTrackerSnapshotDto>());
    }

    [Fact]
    public void Messages_references_only_Domain()
    {
        Assert.Empty(NjordReferences<Njord.Messages.Pipeline.WeightedTarget>().Except(["Njord.Domain"]));
    }

    [Fact]
    public void Core_references_only_Domain_Messages_and_Persistence()
    {
        Assert.Empty(NjordReferences<Njord.Actors.ISchedulerActor>().Except(["Njord.Domain", "Njord.Messages", "Njord.Persistence"]));
    }

    private static readonly string[] CoreAndBelow = ["Njord.Core", "Njord.Domain", "Njord.Messages", "Njord.Persistence"];

    [Fact]
    public void Ingest_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Ingest.OpenMeteoClient>().Except(CoreAndBelow));
    }

    [Fact]
    public void Sensors_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Sensors.SensorHubActor>().Except(CoreAndBelow));
    }

    [Fact]
    public void Grpc_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Grpc.WeatherGrpcService>().Except(CoreAndBelow));
    }

    [Fact]
    public void Pipeline_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Pipeline.PipelineActor>().Except(CoreAndBelow));
    }

    [Fact]
    public void Egress_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Egress.ModelStateActor>().Except(CoreAndBelow));
    }

    [Fact]
    public void Mqtt_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Mqtt.MqttStateActor>().Except(CoreAndBelow));
    }

    [Fact]
    public void Enrichment_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.Enrichment.EnrichmentActor>().Except(CoreAndBelow));
    }
}
