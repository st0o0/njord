using System.Runtime.CompilerServices;
using Njord.Consensus;
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
    public void Forecast_references_only_Model()
    {
        Assert.Empty(NjordReferences<Forecast.Forecast>().Except(["Njord.Model"]));
    }

    [Fact]
    public void Persistence_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Persistence.Events.Budget.PersistedBudgetTrackerState>());
    }

    [Fact]
    public void Messages_references_only_Model_Forecast_and_Consensus()
    {
        Assert.Empty(NjordReferences<Njord.Messages.Pipeline.QueryPollStates>().Except(["Njord.Consensus", "Njord.Forecast", "Njord.Model"]));
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
    public void Consensus_references_only_Model_and_Forecast()
    {
        Assert.Empty(NjordReferences<LocationConsensus>().Except(["Njord.Forecast", "Njord.Model"]));
    }

    [Fact]
    public void Core_references_only_Model_Forecast_Messages_Persistence_and_Consensus()
    {
        Assert.Empty(NjordReferences<IPollManager>().Except(["Njord.Consensus", "Njord.Forecast", "Njord.Model", "Njord.Messages", "Njord.Persistence"]));
    }

    private static readonly string[] CoreAndBelow = ["Njord.Consensus", "Njord.Core", "Njord.Forecast", "Njord.Model", "Njord.Messages", "Njord.Persistence"];

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
    public void Pipeline_references_only_Core_and_analysis_libraries()
    {
        Assert.Empty(NjordReferences<Pipeline.ForecastPipeline>()
            .Except([.. CoreAndBelow, "Njord.Alerts", "Njord.Derived", "Njord.Trends", "Njord.Indices"]));
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

    [Fact]
    public void Derived_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Derived.DerivedAnalyzer>().Except(CoreAndBelow));
    }

    [Fact]
    public void Trends_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Trends.TrendAnalyzer>().Except(CoreAndBelow));
    }

    [Fact]
    public void Indices_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Indices.IndexAnalyzer>().Except(CoreAndBelow));
    }

    [Fact]
    public void Consensus_has_no_Akka_dependency()
    {
        var akka = typeof(ConsensusBuilder).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Akka", StringComparison.Ordinal));

        Assert.Empty(akka);
    }

    [Fact]
    public void Consensus_grants_internals_only_to_its_own_test_project()
    {
        var friends = typeof(ConsensusBuilder).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName);

        Assert.Equal(["Njord.Consensus.Tests"], friends);
    }
}
