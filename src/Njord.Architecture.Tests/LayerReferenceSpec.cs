using System.Runtime.CompilerServices;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
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
    public void Weather_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Weather.Forecast>());
    }

    [Fact]
    public void Sensors_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Sensors.SensorKind>());
    }

    [Fact]
    public void Persistence_references_no_other_Njord_assembly()
    {
        Assert.Empty(NjordReferences<Njord.Persistence.Events.Budget.PersistedBudgetTrackerState>());
    }

    [Fact]
    public void Messages_references_only_Weather_Sensors_and_Compute()
    {
        Assert.Empty(NjordReferences<Njord.Messages.Pipeline.QueryPollStates>().Except(["Njord.Weather", "Njord.Sensors", "Njord.Compute"]));
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
    public void Compute_references_only_Weather_and_Sensors()
    {
        Assert.Empty(NjordReferences<ConsensusSnapshot>().Except(["Njord.Weather", "Njord.Sensors"]));
    }

    [Fact]
    public void Core_references_only_Weather_Sensors_Messages_Persistence_and_Compute()
    {
        Assert.Empty(NjordReferences<IPollManager>().Except(["Njord.Compute", "Njord.Weather", "Njord.Sensors", "Njord.Messages", "Njord.Persistence"]));
    }

    private static readonly string[] CoreAndBelow = ["Njord.Compute", "Njord.Core", "Njord.Weather", "Njord.Sensors", "Njord.Messages", "Njord.Persistence"];

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
    public void SensorHub_references_only_Core_and_below()
    {
        Assert.Empty(NjordReferences<Njord.SensorHub.SensorHub>().Except(CoreAndBelow));
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
        Assert.Empty(NjordReferences<Mqtt.MqttStatePublisher>().Except(CoreAndBelow));
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
    public void Compute_grants_internals_only_to_its_own_test_project()
    {
        var friends = typeof(ConsensusBuilder).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName);

        Assert.Equal(["Njord.Compute.Tests"], friends);
    }
}
