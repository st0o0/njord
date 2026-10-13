using System.Threading.Channels;
using Grpc.Core;
using Njord.Compute.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Njord.Grpc.V2;
using Njord.Weather;

namespace Njord.Grpc;

public sealed class AdminGrpcService(
    ConfigView view,
    IConfigWriter writer,
    ConfigChangeFeed changes) : AdminService.AdminServiceBase
{
    internal const string RestartNoticePrefix = "Takes effect after restart: ";

    public override Task<NjordConfig> GetConfig(GetConfigRequest request, ServerCallContext context)
    {
        return Task.FromResult(MapConfig(view));
    }

    public override async Task StreamConfig(
        StreamConfigRequest request,
        IServerStreamWriter<NjordConfig> responseStream,
        ServerCallContext context)
    {
        var signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });
        using var subscription = changes.Subscribe(() => signals.Writer.TryWrite(true));
        await using var cancellation = context.CancellationToken.Register(() => signals.Writer.TryComplete());

        var last = MapConfig(view);
        await responseStream.WriteAsync(last);

        await foreach (var _ in signals.Reader.ReadAllAsync(CancellationToken.None))
        {
            var current = MapConfig(view);
            if (current.Equals(last))
            {
                continue;
            }

            last = current;
            await responseStream.WriteAsync(current);
        }
    }

    public override async Task<ConfigResponse> SetLocations(SetLocationsRequest request, ServerCallContext context)
    {
        if (request.Locations.Count == 0)
        {
            return Rejected("Cannot set empty location list");
        }

        var locations = MapLocations(request);
        if (BudgetRejection(scope => scope.Locations = locations, view.Polling) is { } budgetRejection)
        {
            return budgetRejection;
        }

        var result = await writer.UpdateAsync<NjordOptions>(opt => opt.Locations = locations, context.CancellationToken);
        return result.Applied
            ? Success(RestartRequired(result, ""))
            : Rejected(string.Join("; ", result.Failures));
    }

    private static List<LocationOptions> MapLocations(SetLocationsRequest request)
    {
        return
        [
            .. request.Locations.Select(l => new LocationOptions
            {
                Name = l.Name,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Models = l.Models.Count > 0 ? [.. l.Models] : null,
            })
        ];
    }

    public override async Task<ConfigResponse> SetSettings(SetSettingsRequest request, ServerCallContext context)
    {
        if (ValidateSettings(request) is { } rejection)
        {
            return rejection;
        }

        var polling = ConfigWriter.DeepClone(view.Polling);
        if (request.HasPollIntervalSeconds)
        {
            polling.PollInterval = TimeSpan.FromSeconds(request.PollIntervalSeconds);
        }

        if (BudgetRejection(scope => ApplyScope(scope, request), polling) is { } budgetRejection)
        {
            return budgetRejection;
        }

        var failures = writer.Check<NjordOptions>(opt => ApplyScope(opt, request));
        if (failures.Count > 0)
        {
            return Rejected(string.Join("; ", failures));
        }

        var scopeResult = await writer.UpdateAsync<NjordOptions>(opt => ApplyScope(opt, request), context.CancellationToken);
        if (!scopeResult.Applied)
        {
            return Rejected(string.Join("; ", scopeResult.Failures));
        }

        if (request.HasPollIntervalSeconds)
        {
            var pollingResult = await writer.UpdateAsync<PollingOptions>(
                opt => opt.PollInterval = polling.PollInterval, context.CancellationToken);
            if (!pollingResult.Applied)
            {
                return Rejected(string.Join("; ", pollingResult.Failures));
            }
        }

        return Success(RestartRequired(scopeResult, ""));
    }

    private static ConfigResponse? ValidateSettings(SetSettingsRequest request)
    {
        if (request is { HasPollIntervalSeconds: true, PollIntervalSeconds: < 60 })
        {
            return Rejected("Poll interval must be at least 60 seconds");
        }

        if (request is { HasForecastDays: true, ForecastDays: < 1 or > 16 })
        {
            return Rejected("Forecast days must be between 1 and 16");
        }

        return null;
    }

    private static void ApplyScope(NjordOptions opt, SetSettingsRequest request)
    {
        if (request.HasForecastDays)
        {
            opt.ForecastDays = request.ForecastDays;
        }

        if (request.Horizons.Count > 0)
        {
            opt.Horizons = [.. request.Horizons];
        }

        if (request.DefaultModels.Count > 0)
        {
            opt.Models = [.. request.DefaultModels];
        }

        if (request.Parameters is not null)
        {
            opt.Parameters = new ParameterOptions
            {
                Groups = [.. request.Parameters.Groups],
                Extra = [.. request.Parameters.Extra],
                Exclude = [.. request.Parameters.Exclude],
            };
        }
    }

    // Rejects a scope or poll interval change whose projection breaks the same 80 % guard that startup validation applies.
    private ConfigResponse? BudgetRejection(Action<NjordOptions> applyScope, PollingOptions polling)
    {
        var scope = ConfigWriter.DeepClone(view.Scope);
        applyScope(scope);

        try
        {
            var projection = BudgetCalculator.Validate(scope, polling);
            return BudgetCalculator.GuardViolation(projection, polling.PollInterval) is not null
                ? Rejected($"Would exceed budget: {projection.UsagePercent:F0}% of monthly limit")
                : null;
        }
        catch (ParameterResolutionException ex)
        {
            return Rejected(string.Join("; ", ex.Errors));
        }
    }

    public override async Task<ConfigResponse> SetEnrichment(SetEnrichmentRequest request, ServerCallContext context)
    {
        var failures = new List<string>();
        if (request.Consensus is { } consensus)
        {
            failures.AddRange(writer.Check<ConsensusOptions>(opt => ApplyConsensus(opt, consensus)));
        }

        if (request.Alerts is { } alerts)
        {
            failures.AddRange(writer.Check<AlertOptions>(opt => ApplyAlerts(opt, alerts)));
        }

        if (request.Indices is { } indices)
        {
            failures.AddRange(writer.Check<IndexOptions>(opt => ApplyIndices(opt, indices)));
        }

        if (failures.Count > 0)
        {
            return Rejected(string.Join("; ", failures));
        }

        var restartRequired = new List<string>();
        var ct = context.CancellationToken;

        if (request.Consensus is { } c)
        {
            restartRequired.AddRange(RestartRequired(
                await writer.UpdateAsync<ConsensusOptions>(opt => ApplyConsensus(opt, c), ct), "Enrichment.Consensus."));
        }

        if (request.Alerts is { } a)
        {
            restartRequired.AddRange(RestartRequired(
                await writer.UpdateAsync<AlertOptions>(opt => ApplyAlerts(opt, a), ct), "Enrichment.Alerts."));
        }

        if (request.Derived is { HasEnabled: true } derived)
        {
            restartRequired.AddRange(RestartRequired(
                await writer.UpdateAsync<DerivedOptions>(opt => opt.Enabled = derived.Enabled, ct), "Enrichment.Derived."));
        }

        if (request.Trends is { HasEnabled: true } trends)
        {
            restartRequired.AddRange(RestartRequired(
                await writer.UpdateAsync<TrendOptions>(opt => opt.Enabled = trends.Enabled, ct), "Enrichment.Trends."));
        }

        if (request.Indices is { } i)
        {
            restartRequired.AddRange(RestartRequired(
                await writer.UpdateAsync<IndexOptions>(opt => ApplyIndices(opt, i), ct), "Enrichment.Indices."));
        }

        return Success(restartRequired);
    }

    private static void ApplyConsensus(ConsensusOptions opt, ConsensusConfig consensus)
    {
        if (consensus.HasEnabled) opt.Enabled = consensus.Enabled;
        if (consensus.HasMethod && Enum.TryParse<ConsensusMethod>(consensus.Method, ignoreCase: true, out var method))
            opt.Method = method;
        if (consensus.HasTrimPercent) opt.TrimPercent = consensus.TrimPercent;
    }

    private static void ApplyAlerts(AlertOptions opt, AlertConfig alerts)
    {
        if (alerts.HasEnabled) opt.Enabled = alerts.Enabled;
        if (alerts.FrostThresholds.Count > 0) opt.FrostThresholds = [.. alerts.FrostThresholds];
        if (alerts.HeatThresholds.Count > 0) opt.HeatThresholds = [.. alerts.HeatThresholds];
        if (alerts.StormGustThresholds.Count > 0) opt.StormGustThresholds = [.. alerts.StormGustThresholds];
        if (alerts.HasHeavyRainHourlyThreshold) opt.HeavyRainHourlyThreshold = alerts.HeavyRainHourlyThreshold;
        if (alerts.HasHeavyRainDailyThreshold) opt.HeavyRainDailyThreshold = alerts.HeavyRainDailyThreshold;
        if (alerts.HasPressureDropThreshold) opt.PressureDropThreshold = alerts.PressureDropThreshold;
        if (alerts.HasCapeThreshold) opt.CapeThreshold = alerts.CapeThreshold;
        if (alerts.HasThunderstormPrecipThreshold) opt.ThunderstormPrecipThreshold = alerts.ThunderstormPrecipThreshold;
        if (alerts.HasThunderstormGustThreshold) opt.ThunderstormGustThreshold = alerts.ThunderstormGustThreshold;
        if (alerts.HasPressureDropSevereThreshold) opt.PressureDropSevereThreshold = alerts.PressureDropSevereThreshold;
        if (alerts.HasFogPersistentHours) opt.FogPersistentHours = alerts.FogPersistentHours;
        if (alerts.HasIceThreshold) opt.IceThreshold = alerts.IceThreshold;
        if (alerts.WindChillThresholds.Count > 0) opt.WindChillThresholds = [.. alerts.WindChillThresholds];
        if (alerts.VisibilityThresholds.Count > 0) opt.VisibilityThresholds = [.. alerts.VisibilityThresholds];
        if (alerts.TropicalNightThresholds.Count > 0) opt.TropicalNightThresholds = [.. alerts.TropicalNightThresholds];
        if (alerts.HumidityThresholds.Count > 0) opt.HumidityThresholds = [.. alerts.HumidityThresholds];
    }

    private static void ApplyIndices(IndexOptions opt, IndexConfig indices)
    {
        if (indices.HasEnabled) opt.Enabled = indices.Enabled;
        if (indices.HasIndoorTemp) opt.Preferences.IndoorTemp = indices.IndoorTemp;
        if (indices.HasIdealOutdoorTemp) opt.Preferences.IdealOutdoorTemp = indices.IdealOutdoorTemp;
        if (indices.HasHeatSensitivity) opt.Preferences.HeatSensitivity = indices.HeatSensitivity;
        if (indices.HasHumiditySensitivity) opt.Preferences.HumiditySensitivity = indices.HumiditySensitivity;
        if (indices.HasWindSensitivity) opt.Preferences.WindSensitivity = indices.WindSensitivity;
        if (indices.HasRainSensitivity) opt.Preferences.RainSensitivity = indices.RainSensitivity;
        if (indices.HasRunningIdealTempLow) opt.Preferences.RunningIdealTempLow = indices.RunningIdealTempLow;
        if (indices.HasRunningIdealTempHigh) opt.Preferences.RunningIdealTempHigh = indices.RunningIdealTempHigh;
        if (indices.HasBbqMinTemp) opt.Preferences.BbqMinTemp = indices.BbqMinTemp;
        if (indices.HasBbqIdealWindLow) opt.Preferences.BbqIdealWindLow = indices.BbqIdealWindLow;
        if (indices.HasBbqIdealWindHigh) opt.Preferences.BbqIdealWindHigh = indices.BbqIdealWindHigh;
    }

    public override async Task<ConfigResponse> SetBudget(SetBudgetRequest request, ServerCallContext context)
    {
        if (request is { HasRequestsPerMonth: true, RequestsPerMonth: <= 0 })
        {
            return Rejected("requests_per_month must be greater than zero");
        }

        if (request is { HasRequestsPerMinute: true, RequestsPerMinute: <= 0 })
        {
            return Rejected("requests_per_minute must be greater than zero");
        }

        var result = await writer.UpdateAsync<PollingOptions>(opt =>
        {
            if (request.HasRequestsPerMonth || request.HasRequestsPerMinute)
            {
                var current = BudgetCalculator.GetEffectiveBudget(opt);
                opt.BudgetOverride = new RequestBudget(
                    request.HasRequestsPerMonth ? request.RequestsPerMonth : current.RequestsPerMonth,
                    request.HasRequestsPerMinute ? request.RequestsPerMinute : current.RequestsPerMinute);
            }
            else
            {
                opt.BudgetOverride = null;
            }
        }, context.CancellationToken);

        return result.Applied
            ? Success([])
            : Rejected(string.Join("; ", result.Failures));
    }

    internal static NjordConfig MapConfig(ConfigView view)
    {
        var options = view.Scope;
        var polling = view.Polling;
        var budget = BudgetCalculator.Validate(options, polling);

        var config = new NjordConfig
        {
            ForecastDays = options.ForecastDays,
            PollIntervalSeconds = (long)polling.PollInterval.TotalSeconds,
            Parameters = new ParameterConfig(),
            Enrichment = MapEnrichment(view),
            BudgetProjection = MapBudgetProjection(budget),
        };

        if (polling.BudgetOverride is { } bo)
        {
            config.BudgetOverride = new BudgetConfig
            {
                RequestsPerMonth = bo.RequestsPerMonth,
                RequestsPerMinute = bo.RequestsPerMinute,
            };
        }

        config.DefaultModels.AddRange(options.Models);
        config.Horizons.AddRange(options.Horizons);
        config.Parameters.Groups.AddRange(options.Parameters.Groups);
        config.Parameters.Extra.AddRange(options.Parameters.Extra);
        config.Parameters.Exclude.AddRange(options.Parameters.Exclude);

        foreach (var loc in options.Locations)
        {
            var locationInfo = new LocationInfo
            {
                Name = loc.Name,
                Latitude = loc.Latitude,
                Longitude = loc.Longitude,
            };
            locationInfo.Models.AddRange(options.Models.Union(loc.Models ?? [], StringComparer.OrdinalIgnoreCase));
            config.Locations.Add(locationInfo);
        }

        return config;
    }

    private static DetailedEnrichmentConfig MapEnrichment(ConfigView view)
    {
        return new DetailedEnrichmentConfig
        {
            Consensus = new ConsensusConfig
            {
                Enabled = view.Consensus.Enabled,
                Method = view.Consensus.Method.ToString(),
                TrimPercent = view.Consensus.TrimPercent,
            },
            Alerts = new AlertConfig
            {
                Enabled = view.Alerts.Enabled,
                FrostThresholds = { view.Alerts.FrostThresholds },
                HeatThresholds = { view.Alerts.HeatThresholds },
                StormGustThresholds = { view.Alerts.StormGustThresholds },
                HeavyRainHourlyThreshold = view.Alerts.HeavyRainHourlyThreshold,
                HeavyRainDailyThreshold = view.Alerts.HeavyRainDailyThreshold,
                PressureDropThreshold = view.Alerts.PressureDropThreshold,
                CapeThreshold = view.Alerts.CapeThreshold,
                ThunderstormPrecipThreshold = view.Alerts.ThunderstormPrecipThreshold,
                ThunderstormGustThreshold = view.Alerts.ThunderstormGustThreshold,
                PressureDropSevereThreshold = view.Alerts.PressureDropSevereThreshold,
                FogPersistentHours = view.Alerts.FogPersistentHours,
                IceThreshold = view.Alerts.IceThreshold,
                WindChillThresholds = { view.Alerts.WindChillThresholds },
                VisibilityThresholds = { view.Alerts.VisibilityThresholds },
                TropicalNightThresholds = { view.Alerts.TropicalNightThresholds },
                HumidityThresholds = { view.Alerts.HumidityThresholds },
            },
            Derived = new DerivedConfig { Enabled = view.Derived.Enabled },
            Trends = new TrendConfig { Enabled = view.Trends.Enabled },
            Indices = new IndexConfig
            {
                Enabled = view.Indices.Enabled,
                IndoorTemp = view.Indices.Preferences.IndoorTemp ?? 22.0,
                IdealOutdoorTemp = view.Indices.Preferences.IdealOutdoorTemp ?? 22.0,
                HeatSensitivity = view.Indices.Preferences.HeatSensitivity ?? 1.0,
                HumiditySensitivity = view.Indices.Preferences.HumiditySensitivity ?? 1.0,
                WindSensitivity = view.Indices.Preferences.WindSensitivity ?? 1.0,
                RainSensitivity = view.Indices.Preferences.RainSensitivity ?? 1.0,
                RunningIdealTempLow = view.Indices.Preferences.RunningIdealTempLow ?? 5.0,
                RunningIdealTempHigh = view.Indices.Preferences.RunningIdealTempHigh ?? 20.0,
                BbqMinTemp = view.Indices.Preferences.BbqMinTemp ?? 10.0,
                BbqIdealWindLow = view.Indices.Preferences.BbqIdealWindLow ?? 1.0,
                BbqIdealWindHigh = view.Indices.Preferences.BbqIdealWindHigh ?? 3.0,
            },
        };
    }

    private static BudgetProjection MapBudgetProjection(BudgetValidation validation)
    {
        return new BudgetProjection
        {
            ProjectedMonthlyCalls = validation.ProjectedMonthlyCalls,
            MonthlyLimit = validation.MonthlyLimit,
            UsagePercent = validation.UsagePercent,
            WithinBudget = validation.WithinBudget,
        };
    }

    private static ConfigResponse Rejected(string reason)
    {
        return new ConfigResponse
        {
            Applied = false,
            RejectionReason = reason,
        };
    }

    private ConfigResponse Success(IEnumerable<string> restartRequired)
    {
        var config = MapConfig(view);
        var response = new ConfigResponse
        {
            Applied = true,
            Config = config,
            BudgetProjection = config.BudgetProjection.Clone(),
        };
        response.Warnings.AddRange(BudgetCalculator.Validate(view.Scope, view.Polling).Warnings);

        var pending = restartRequired.ToList();
        if (pending.Count > 0)
        {
            response.Warnings.Add($"{RestartNoticePrefix}{string.Join(", ", pending)}");
        }

        return response;
    }

    private static IEnumerable<string> RestartRequired<T>(ConfigUpdateResult<T> result, string section)
        where T : class =>
        result.RequiresRestart
            ? result.ChangedProperties
                .Where(p => ConfigReloadPolicy.For<T>(p) == ReloadMode.RestartRequired)
                .Select(p => $"{section}{p}")
            : [];
}
