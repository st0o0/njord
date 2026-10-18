using Njord.Grpc.V2;
using Contracts = Njord.Messages.Config;
using DomainSection = Njord.Messages.Config.EnrichmentSection;
using ProtoSection = Njord.Grpc.V2.EnrichmentSection;

namespace Njord.Grpc.Mapping;

internal static class ConfigRequestMappings
{
    public static Contracts.UpdateLocations ToCommand(this SetLocationsRequest request) =>
        new([
            .. request.Locations.Select(l => new Contracts.LocationInput(l.Name, l.Latitude, l.Longitude, [.. l.Models]))
        ]);

    public static Contracts.UpdateSettings ToCommand(this SetSettingsRequest request) =>
        new(
            request.HasForecastDays ? request.ForecastDays : null,
            [.. request.Horizons],
            [.. request.DefaultModels],
            request.Parameters is { } parameters
                ? new Contracts.ParameterSelection([.. parameters.Groups], [.. parameters.Extra], [.. parameters.Exclude])
                : null,
            request.HasPollIntervalSeconds ? TimeSpan.FromSeconds(request.PollIntervalSeconds) : null);

    public static Contracts.UpdateAnalysisConfig ToCommand(this SetEnrichmentRequest request) =>
        new(
            request.Consensus?.ToCommand(),
            request.Alerts?.ToCommand(),
            request.Derived is { HasEnabled: true } derived ? derived.Enabled : null,
            request.Trends is { HasEnabled: true } trends ? trends.Enabled : null,
            request.Indices?.ToCommand(),
            [.. request.Reset.Select(section => section.ToDomain())],
            request.DryRun);

    public static DomainSection ToDomain(this ProtoSection section) => section switch
    {
        ProtoSection.Consensus => DomainSection.Consensus,
        ProtoSection.Alerts => DomainSection.Alerts,
        ProtoSection.Derived => DomainSection.Derived,
        ProtoSection.Trends => DomainSection.Trends,
        ProtoSection.Indices => DomainSection.Indices,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };

    public static Contracts.UpdateBudget ToCommand(this SetBudgetRequest request) =>
        new(
            request.HasRequestsPerMonth ? request.RequestsPerMonth : null,
            request.HasRequestsPerMinute ? request.RequestsPerMinute : null);

    private static Contracts.ConsensusConfigUpdate ToCommand(this ConsensusConfig consensus) =>
        new(
            consensus.HasEnabled ? consensus.Enabled : null,
            consensus.HasMethod ? consensus.Method.ToDomain() : null,
            consensus.HasTrimPercent ? consensus.TrimPercent : null);

    private static Contracts.AlertConfigUpdate ToCommand(this AlertConfig alerts) =>
        new(
            alerts.HasEnabled ? alerts.Enabled : null,
            NullIfEmpty(alerts.FrostThresholds),
            NullIfEmpty(alerts.HeatThresholds),
            NullIfEmpty(alerts.StormGustThresholds),
            alerts.HasHeavyRainHourlyThreshold ? alerts.HeavyRainHourlyThreshold : null,
            alerts.HasHeavyRainDailyThreshold ? alerts.HeavyRainDailyThreshold : null,
            alerts.HasPressureDropThreshold ? alerts.PressureDropThreshold : null,
            alerts.HasCapeThreshold ? alerts.CapeThreshold : null,
            alerts.HasThunderstormPrecipThreshold ? alerts.ThunderstormPrecipThreshold : null,
            alerts.HasThunderstormGustThreshold ? alerts.ThunderstormGustThreshold : null,
            alerts.HasPressureDropSevereThreshold ? alerts.PressureDropSevereThreshold : null,
            alerts.HasFogPersistentHours ? alerts.FogPersistentHours : null,
            alerts.HasIceThreshold ? alerts.IceThreshold : null,
            NullIfEmpty(alerts.WindChillThresholds),
            NullIfEmpty(alerts.VisibilityThresholds),
            NullIfEmpty(alerts.TropicalNightThresholds),
            NullIfEmpty(alerts.HumidityThresholds));

    private static Contracts.IndexConfigUpdate ToCommand(this IndexConfig indices) =>
        new(
            indices.HasEnabled ? indices.Enabled : null,
            indices.HasIndoorTemp ? indices.IndoorTemp : null,
            indices.HasIdealOutdoorTemp ? indices.IdealOutdoorTemp : null,
            indices.HasHeatSensitivity ? indices.HeatSensitivity : null,
            indices.HasHumiditySensitivity ? indices.HumiditySensitivity : null,
            indices.HasWindSensitivity ? indices.WindSensitivity : null,
            indices.HasRainSensitivity ? indices.RainSensitivity : null,
            indices.HasRunningIdealTempLow ? indices.RunningIdealTempLow : null,
            indices.HasRunningIdealTempHigh ? indices.RunningIdealTempHigh : null,
            indices.HasBbqMinTemp ? indices.BbqMinTemp : null,
            indices.HasBbqIdealWindLow ? indices.BbqIdealWindLow : null,
            indices.HasBbqIdealWindHigh ? indices.BbqIdealWindHigh : null);

    private static List<double>? NullIfEmpty(IReadOnlyCollection<double> values) =>
        values.Count > 0 ? [.. values] : null;
}
