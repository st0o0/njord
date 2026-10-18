using Njord.Grpc.V2;
using Contracts = Njord.Messages.Config;
using DomainSection = Njord.Messages.Config.EnrichmentSection;
using ProtoSection = Njord.Grpc.V2.EnrichmentSection;

namespace Njord.Grpc.Mapping;

internal static class ConfigResponseMappings
{
    public static NjordConfig ToProto(this Contracts.EffectiveConfig effective)
    {
        var scope = effective.Scope;
        var polling = effective.Polling;

        var config = new NjordConfig
        {
            ForecastDays = scope.ForecastDays,
            PollIntervalSeconds = (long)polling.PollInterval.TotalSeconds,
            Parameters = new ParameterConfig(),
            Enrichment = effective.Analysis.ToProto(),
            BudgetProjection = effective.BudgetProjection.ToProto(),
        };

        if (polling.BudgetOverride is { } budgetOverride)
        {
            config.BudgetOverride = new BudgetConfig
            {
                RequestsPerMonth = budgetOverride.RequestsPerMonth,
                RequestsPerMinute = budgetOverride.RequestsPerMinute,
            };
        }

        config.DefaultModels.AddRange(scope.Models);
        config.Horizons.AddRange(scope.Horizons);
        config.Parameters.Groups.AddRange(scope.Parameters.Groups);
        config.Parameters.Extra.AddRange(scope.Parameters.Extra);
        config.Parameters.Exclude.AddRange(scope.Parameters.Exclude);

        foreach (var location in scope.Locations)
        {
            var locationInfo = new LocationInfo
            {
                Name = location.Id.Name,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
            };
            locationInfo.Models.AddRange(location.ResolvedModels);
            config.Locations.Add(locationInfo);
        }

        return config;
    }

    public static NjordConfig ToProto(this Contracts.ConfigResponse response) =>
        response switch
        {
            Contracts.ConfigResult result => result.Config.ToProto(),
            Contracts.ConfigFailed failed => ConfigResponses.Rethrow<NjordConfig>(failed.Cause),
            _ => throw ConfigResponses.Unknown(response),
        };

    public static ProtoSection ToProto(this DomainSection? section) => section switch
    {
        null => ProtoSection.Unspecified,
        DomainSection.Consensus => ProtoSection.Consensus,
        DomainSection.Alerts => ProtoSection.Alerts,
        DomainSection.Derived => ProtoSection.Derived,
        DomainSection.Trends => ProtoSection.Trends,
        DomainSection.Indices => ProtoSection.Indices,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };

    public static ConfigResponse ToProto(this Contracts.UpdateLocationsResponse response) =>
        response switch
        {
            Contracts.UpdateLocationsCompleted completed => ConfigResponses.Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateLocationsRejected rejected => ConfigResponses.Rejected(rejected.Reason),
            Contracts.UpdateLocationsFailed failed => ConfigResponses.Rethrow<ConfigResponse>(failed.Cause),
            _ => throw ConfigResponses.Unknown(response),
        };

    public static ConfigResponse ToProto(this Contracts.UpdateSettingsResponse response) =>
        response switch
        {
            Contracts.UpdateSettingsCompleted completed => ConfigResponses.Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateSettingsRejected rejected => ConfigResponses.Rejected(rejected.Reason),
            Contracts.UpdateSettingsFailed failed => ConfigResponses.Rethrow<ConfigResponse>(failed.Cause),
            _ => throw ConfigResponses.Unknown(response),
        };

    public static ConfigResponse ToProto(this Contracts.UpdateAnalysisConfigResponse response) =>
        response switch
        {
            Contracts.UpdateAnalysisConfigCompleted completed => ConfigResponses.Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateAnalysisConfigValidated validated => ConfigResponses.Validated(validated.Config, validated.RestartRequired),
            Contracts.UpdateAnalysisConfigRejected rejected => ConfigResponses.Rejected(rejected.Issues, rejected.DryRun),
            Contracts.UpdateAnalysisConfigFailed failed => ConfigResponses.Rethrow<ConfigResponse>(failed.Cause),
            _ => throw ConfigResponses.Unknown(response),
        };

    public static ConfigResponse ToProto(this Contracts.UpdateBudgetResponse response) =>
        response switch
        {
            Contracts.UpdateBudgetCompleted completed => ConfigResponses.Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateBudgetRejected rejected => ConfigResponses.Rejected(rejected.Reason),
            Contracts.UpdateBudgetFailed failed => ConfigResponses.Rethrow<ConfigResponse>(failed.Cause),
            _ => throw ConfigResponses.Unknown(response),
        };

    private static DetailedEnrichmentConfig ToProto(this Contracts.AnalysisSections analysis)
    {
        var alerts = analysis.Alerts;
        var preferences = analysis.Indices.Preferences;
        return new DetailedEnrichmentConfig
        {
            Consensus = new ConsensusConfig
            {
                Enabled = analysis.Consensus.Enabled,
                Method = analysis.Consensus.Method.ToProto(),
                TrimPercent = analysis.Consensus.TrimPercent,
            },
            Alerts = new AlertConfig
            {
                Enabled = alerts.Enabled,
                FrostThresholds = { alerts.FrostThresholds },
                HeatThresholds = { alerts.HeatThresholds },
                StormGustThresholds = { alerts.StormGustThresholds },
                HeavyRainHourlyThreshold = alerts.HeavyRainHourlyThreshold,
                HeavyRainDailyThreshold = alerts.HeavyRainDailyThreshold,
                PressureDropThreshold = alerts.PressureDropThreshold,
                CapeThreshold = alerts.CapeThreshold,
                ThunderstormPrecipThreshold = alerts.ThunderstormPrecipThreshold,
                ThunderstormGustThreshold = alerts.ThunderstormGustThreshold,
                PressureDropSevereThreshold = alerts.PressureDropSevereThreshold,
                FogPersistentHours = alerts.FogPersistentHours,
                IceThreshold = alerts.IceThreshold,
                WindChillThresholds = { alerts.WindChillThresholds },
                VisibilityThresholds = { alerts.VisibilityThresholds },
                TropicalNightThresholds = { alerts.TropicalNightThresholds },
                HumidityThresholds = { alerts.HumidityThresholds },
            },
            Derived = new DerivedConfig { Enabled = analysis.Derived.Enabled },
            Trends = new TrendConfig { Enabled = analysis.Trends.Enabled },
            Indices = new IndexConfig
            {
                Enabled = analysis.Indices.Enabled,
                IndoorTemp = preferences.IndoorTemp ?? 22.0,
                IdealOutdoorTemp = preferences.IdealOutdoorTemp ?? 22.0,
                HeatSensitivity = preferences.HeatSensitivity ?? 1.0,
                HumiditySensitivity = preferences.HumiditySensitivity ?? 1.0,
                WindSensitivity = preferences.WindSensitivity ?? 1.0,
                RainSensitivity = preferences.RainSensitivity ?? 1.0,
                RunningIdealTempLow = preferences.RunningIdealTempLow ?? 5.0,
                RunningIdealTempHigh = preferences.RunningIdealTempHigh ?? 20.0,
                BbqMinTemp = preferences.BbqMinTemp ?? 10.0,
                BbqIdealWindLow = preferences.BbqIdealWindLow ?? 1.0,
                BbqIdealWindHigh = preferences.BbqIdealWindHigh ?? 3.0,
            },
        };
    }

    private static BudgetProjection ToProto(this Contracts.BudgetProjection projection) =>
        new()
        {
            ProjectedMonthlyCalls = projection.ProjectedMonthlyCalls,
            MonthlyLimit = projection.MonthlyLimit,
            UsagePercent = projection.UsagePercent,
            WithinBudget = projection.WithinBudget,
        };
}
