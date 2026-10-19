using Njord.Core.Actors;

namespace Njord.Architecture.Tests;

public sealed class LayerReferenceSpec
{
    private static string[] NjordReferences<T>()
        =>
        [
            .. typeof(T).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(n => n.StartsWith("Njord", StringComparison.Ordinal))
                .Order()
        ];

    [Fact]
    public void Model_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Model.ParameterDef>());
    }

    [Fact]
    public void Persistence_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Persistence.Events.Budget.PersistedBudgetTrackerState>());
    }

    [Fact]
    public void Messages_references_only_Model()
    {
        Assert.Empty(NjordReferences<Njord.Messages.Pipeline.QueryPollStates>().Except(["Njord.Model"]));
    }

    [Fact]
    public void Messages_has_no_Akka_dependency()
    {
        var akka = typeof(Njord.Messages.Pipeline.QueryPollStates).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Akka", StringComparison.Ordinal));

        Assert.Empty(akka);
    }

    [Fact]
    public void Core_references_only_Model_Messages_and_Persistence()
    {
        Assert.Empty(NjordReferences<IPollManager>().Except(["Njord.Model", "Njord.Messages", "Njord.Persistence"]));
    }

    private static readonly string[] CoreAndBelow = ["Njord.Core", "Njord.Model", "Njord.Messages", "Njord.Persistence"];

    [Fact]
    public void OpenMeteo_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<OpenMeteo.OpenMeteoClient>().Except(CoreAndBelow));
    }

    [Fact]
    public void Budget_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Budget.BudgetTracker>().Except(CoreAndBelow));
    }

    [Fact]
    public void Grpc_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Grpc.WeatherGrpcService>().Except(CoreAndBelow));
    }

    [Fact]
    public void Pipeline_references_only_Core_and_the_alerts_library()
    {
        Assert.Empty(NjordReferences<Pipeline.ForecastPipeline>()
            .Except([.. CoreAndBelow, "Njord.Alerts"]));
    }

    [Fact]
    public void Mqtt_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Mqtt.Actors.MqttStatePublisher>().Except(CoreAndBelow));
    }

    [Fact]
    public void Alerts_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Alerts.AlertAnalyzer>().Except(CoreAndBelow));
    }
}
