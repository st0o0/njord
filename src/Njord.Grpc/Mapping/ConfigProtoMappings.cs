using System.Runtime.ExceptionServices;
using Njord.Compute.Settings;
using Njord.Grpc.V2;
using Contracts = Njord.Messages.Config;
using DomainSection = Njord.Messages.Config.EnrichmentSection;
using ProtoSection = Njord.Grpc.V2.EnrichmentSection;

namespace Njord.Grpc.Mapping;

internal static class ConfigProtoMappings
{
    public static NjordConfig ToProto(this Contracts.ConfigSnapshot snapshot)
    {
        var scope = snapshot.Scope;
        var polling = snapshot.Polling;

        var config = new NjordConfig
        {
            ForecastDays = scope.ForecastDays,
            PollIntervalSeconds = (long)polling.PollInterval.TotalSeconds,
            Parameters = new ParameterConfig(),
            Enrichment = snapshot.Analysis.ToProto(),
            BudgetProjection = snapshot.BudgetProjection.ToProto(),
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
            Contracts.ConfigFailed failed => Rethrow<NjordConfig>(failed.Cause),
            _ => throw Unknown(response),
        };

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

    // Wire values the command cannot express are reported as issues before the configuration manager is asked.
    public static ConfigResponse? RejectUnknownValues(this SetEnrichmentRequest request)
    {
        List<Contracts.ConfigIssue> issues = [];
        if (request.Consensus is { HasMethod: true } consensus && !Enum.IsDefined(consensus.Method))
        {
            issues.Add(new Contracts.ConfigIssue(
                DomainSection.Consensus, $"Unknown consensus method: {(int)consensus.Method}"));
        }

        issues.AddRange(request.Reset
            .Where(section => section == ProtoSection.Unspecified || !Enum.IsDefined(section))
            .Select(section => new Contracts.ConfigIssue(
                null, $"Unknown or unspecified section in reset: {(int)section}")));

        return issues.Count > 0 ? Rejected(issues, request.DryRun) : null;
    }

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

    public static ConfigResponse ToProto(this Contracts.UpdateLocationsResponse response) =>
        response switch
        {
            Contracts.UpdateLocationsCompleted completed => Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateLocationsRejected rejected => Rejected(rejected.Reason),
            Contracts.UpdateLocationsFailed failed => Rethrow<ConfigResponse>(failed.Cause),
            _ => throw Unknown(response),
        };

    public static ConfigResponse ToProto(this Contracts.UpdateSettingsResponse response) =>
        response switch
        {
            Contracts.UpdateSettingsCompleted completed => Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateSettingsRejected rejected => Rejected(rejected.Reason),
            Contracts.UpdateSettingsFailed failed => Rethrow<ConfigResponse>(failed.Cause),
            _ => throw Unknown(response),
        };

    public static ConfigResponse ToProto(this Contracts.UpdateAnalysisConfigResponse response) =>
        response switch
        {
            Contracts.UpdateAnalysisConfigCompleted completed => Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateAnalysisConfigValidated validated => Validated(validated.Config, validated.RestartRequired),
            Contracts.UpdateAnalysisConfigRejected rejected => Rejected(rejected.Issues, rejected.DryRun),
            Contracts.UpdateAnalysisConfigFailed failed => Rethrow<ConfigResponse>(failed.Cause),
            _ => throw Unknown(response),
        };

    public static ConfigResponse ToProto(this Contracts.UpdateBudgetResponse response) =>
        response switch
        {
            Contracts.UpdateBudgetCompleted completed => Applied(completed.Config, completed.RestartRequired),
            Contracts.UpdateBudgetRejected rejected => Rejected(rejected.Reason),
            Contracts.UpdateBudgetFailed failed => Rethrow<ConfigResponse>(failed.Cause),
            _ => throw Unknown(response),
        };

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

    private static ConfigResponse Applied(Contracts.ConfigSnapshot snapshot, IReadOnlyList<string> restartRequired)
    {
        var config = snapshot.ToProto();
        var response = new ConfigResponse
        {
            Applied = true,
            Config = config,
            BudgetProjection = config.BudgetProjection.Clone(),
        };
        response.Warnings.AddRange(snapshot.BudgetProjection.Warnings);
        response.RestartRequired.AddRange(restartRequired);
        return response;
    }

    private static ConfigResponse Validated(Contracts.ConfigSnapshot snapshot, IReadOnlyList<string> restartRequired)
    {
        var response = Applied(snapshot, restartRequired);
        response.Applied = false;
        response.DryRun = true;
        return response;
    }

    private static ConfigResponse Rejected(IReadOnlyList<Contracts.ConfigIssue> issues, bool dryRun)
    {
        var response = Rejected(string.Join("; ", issues.Select(i => i.Message)));
        response.DryRun = dryRun;
        response.Issues.AddRange(issues.Select(i => new ConfigIssue
        {
            Section = i.Section.ToProto(),
            Message = i.Message,
        }));
        return response;
    }

    private static ConfigResponse Rejected(string reason) =>
        new()
        {
            Applied = false,
            RejectionReason = reason,
        };

    // A failure inside the configuration manager surfaces as it did when the API wrote the configuration itself.
    private static T Rethrow<T>(Exception cause)
    {
        ExceptionDispatchInfo.Capture(cause).Throw();
        return default!;
    }

    private static ArgumentOutOfRangeException Unknown(object response) =>
        new(nameof(response), response.GetType().Name, "Unknown configuration response");
}
