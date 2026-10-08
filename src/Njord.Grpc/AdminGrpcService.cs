using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Compute.Configuration;
using Njord.Core.Configuration;
using Njord.Domain.Options;
using Njord.Grpc.V2;

namespace Njord.Grpc;

public sealed class AdminGrpcService(
    IOptionsMonitor<NjordOptions> optionsMonitor,
    IWritableOptions<NjordOptions> writableOptions) : AdminService.AdminServiceBase
{
    public override Task<NjordConfig> GetConfig(GetConfigRequest request, ServerCallContext context)
    {
        return Task.FromResult(MapConfig(optionsMonitor.CurrentValue));
    }

    public override async Task StreamConfig(
        StreamConfigRequest request,
        IServerStreamWriter<NjordConfig> responseStream,
        ServerCallContext context)
    {
        await responseStream.WriteAsync(MapConfig(optionsMonitor.CurrentValue));

        var tcs = new TaskCompletionSource();
        await using var registration = context.CancellationToken.Register(() => tcs.TrySetResult());

        using var onChange = optionsMonitor.OnChange(async (options, _) =>
        {
            if (!context.CancellationToken.IsCancellationRequested)
            {
                await responseStream.WriteAsync(MapConfig(options));
            }
        });

        await tcs.Task;
    }

    public override Task<ConfigResponse> SetLocations(SetLocationsRequest request, ServerCallContext context)
    {
        if (request.Locations.Count == 0)
        {
            return Task.FromResult(Rejected("Cannot set empty location list"));
        }

        var snapshot = writableOptions.Update(opt =>
        {
            opt.Locations = request.Locations.Select(l => new LocationOptions
            {
                Name = l.Name,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Models = l.Models.Count > 0 ? [.. l.Models] : null,
            }).ToList();
        });

        var budget = BudgetCalculator.Validate(snapshot);
        if (!budget.WithinBudget)
        {
            writableOptions.Update(opt =>
            {
                opt.Locations = optionsMonitor.CurrentValue.Locations.ToList();
            });
            return Task.FromResult(Rejected($"Would exceed budget: {budget.UsagePercent:F0}% of monthly limit"));
        }

        return Task.FromResult(Success(snapshot, budget));
    }

    public override Task<ConfigResponse> SetSettings(SetSettingsRequest request, ServerCallContext context)
    {
        if (request.HasPollIntervalSeconds && request.PollIntervalSeconds < 60)
        {
            return Task.FromResult(Rejected("Poll interval must be at least 60 seconds"));
        }

        if (request.HasForecastDays && request.ForecastDays is < 1 or > 16)
        {
            return Task.FromResult(Rejected("Forecast days must be between 1 and 16"));
        }

        var snapshot = writableOptions.Update(opt =>
        {
            if (request.HasPollIntervalSeconds)
            {
                opt.PollInterval = TimeSpan.FromSeconds(request.PollIntervalSeconds);
            }

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
        });

        var budget = BudgetCalculator.Validate(snapshot);
        if (!budget.WithinBudget)
        {
            writableOptions.Update(_ => { });
            return Task.FromResult(Rejected($"Would exceed budget: {budget.UsagePercent:F0}% of monthly limit"));
        }

        return Task.FromResult(Success(snapshot, budget));
    }

    public override Task<ConfigResponse> SetEnrichment(SetEnrichmentRequest request, ServerCallContext context)
    {
        var snapshot = writableOptions.Update(opt =>
        {
            if (request.Consensus is { } consensus)
            {
                if (consensus.HasEnabled) opt.Enrichment.Consensus.Enabled = consensus.Enabled;
                if (consensus.HasMethod) opt.Enrichment.Consensus.Method = consensus.Method;
                if (consensus.HasTrimPercent) opt.Enrichment.Consensus.TrimPercent = consensus.TrimPercent;
            }

            if (request.Alerts is { } alerts)
            {
                if (alerts.HasEnabled) opt.Enrichment.Alerts.Enabled = alerts.Enabled;
                if (alerts.FrostThresholds.Count > 0) opt.Enrichment.Alerts.FrostThresholds = [.. alerts.FrostThresholds];
                if (alerts.HeatThresholds.Count > 0) opt.Enrichment.Alerts.HeatThresholds = [.. alerts.HeatThresholds];
                if (alerts.StormGustThresholds.Count > 0) opt.Enrichment.Alerts.StormGustThresholds = [.. alerts.StormGustThresholds];
                if (alerts.HasHeavyRainHourlyThreshold) opt.Enrichment.Alerts.HeavyRainHourlyThreshold = alerts.HeavyRainHourlyThreshold;
                if (alerts.HasHeavyRainDailyThreshold) opt.Enrichment.Alerts.HeavyRainDailyThreshold = alerts.HeavyRainDailyThreshold;
                if (alerts.HasPressureDropThreshold) opt.Enrichment.Alerts.PressureDropThreshold = alerts.PressureDropThreshold;
                if (alerts.HasCapeThreshold) opt.Enrichment.Alerts.CapeThreshold = alerts.CapeThreshold;
                if (alerts.HasThunderstormPrecipThreshold) opt.Enrichment.Alerts.ThunderstormPrecipThreshold = alerts.ThunderstormPrecipThreshold;
                if (alerts.HasThunderstormGustThreshold) opt.Enrichment.Alerts.ThunderstormGustThreshold = alerts.ThunderstormGustThreshold;
                if (alerts.HasPressureDropSevereThreshold) opt.Enrichment.Alerts.PressureDropSevereThreshold = alerts.PressureDropSevereThreshold;
                if (alerts.HasFogPersistentHours) opt.Enrichment.Alerts.FogPersistentHours = alerts.FogPersistentHours;
                if (alerts.HasIceThreshold) opt.Enrichment.Alerts.IceThreshold = alerts.IceThreshold;
                if (alerts.WindChillThresholds.Count > 0) opt.Enrichment.Alerts.WindChillThresholds = [.. alerts.WindChillThresholds];
                if (alerts.VisibilityThresholds.Count > 0) opt.Enrichment.Alerts.VisibilityThresholds = [.. alerts.VisibilityThresholds];
                if (alerts.TropicalNightThresholds.Count > 0) opt.Enrichment.Alerts.TropicalNightThresholds = [.. alerts.TropicalNightThresholds];
                if (alerts.HumidityThresholds.Count > 0) opt.Enrichment.Alerts.HumidityThresholds = [.. alerts.HumidityThresholds];
            }

            if (request.Derived is { } derived)
            {
                if (derived.HasEnabled) opt.Enrichment.Derived.Enabled = derived.Enabled;
            }

            if (request.Trends is { } trends)
            {
                if (trends.HasEnabled) opt.Enrichment.Trends.Enabled = trends.Enabled;
            }

            if (request.Indices is { } indices)
            {
                if (indices.HasEnabled) opt.Enrichment.Indices.Enabled = indices.Enabled;
                if (indices.HasIndoorTemp) opt.Enrichment.Indices.Preferences.IndoorTemp = indices.IndoorTemp;
                if (indices.HasIdealOutdoorTemp) opt.Enrichment.Indices.Preferences.IdealOutdoorTemp = indices.IdealOutdoorTemp;
                if (indices.HasHeatSensitivity) opt.Enrichment.Indices.Preferences.HeatSensitivity = indices.HeatSensitivity;
                if (indices.HasHumiditySensitivity) opt.Enrichment.Indices.Preferences.HumiditySensitivity = indices.HumiditySensitivity;
                if (indices.HasWindSensitivity) opt.Enrichment.Indices.Preferences.WindSensitivity = indices.WindSensitivity;
                if (indices.HasRainSensitivity) opt.Enrichment.Indices.Preferences.RainSensitivity = indices.RainSensitivity;
                if (indices.HasRunningIdealTempLow) opt.Enrichment.Indices.Preferences.RunningIdealTempLow = indices.RunningIdealTempLow;
                if (indices.HasRunningIdealTempHigh) opt.Enrichment.Indices.Preferences.RunningIdealTempHigh = indices.RunningIdealTempHigh;
                if (indices.HasBbqMinTemp) opt.Enrichment.Indices.Preferences.BbqMinTemp = indices.BbqMinTemp;
                if (indices.HasBbqIdealWindLow) opt.Enrichment.Indices.Preferences.BbqIdealWindLow = indices.BbqIdealWindLow;
                if (indices.HasBbqIdealWindHigh) opt.Enrichment.Indices.Preferences.BbqIdealWindHigh = indices.BbqIdealWindHigh;
            }

            if (request.History is { } history)
            {
                if (history.HasEnabled) opt.Enrichment.History.Enabled = history.Enabled;
                if (history.HasRetentionDays) opt.Enrichment.History.RetentionDays = history.RetentionDays;
                if (history.HasMinSampleSize) opt.Enrichment.History.MinSampleSize = history.MinSampleSize;
                if (history.HasSnapshotInterval) opt.Enrichment.History.SnapshotInterval = history.SnapshotInterval;
            }
        });

        var budget = BudgetCalculator.Validate(snapshot);
        return Task.FromResult(Success(snapshot, budget));
    }

    public override Task<ConfigResponse> SetBudget(SetBudgetRequest request, ServerCallContext context)
    {
        var snapshot = writableOptions.Update(opt =>
        {
            if (request.HasRequestsPerMonth || request.HasRequestsPerMinute)
            {
                var current = opt.BudgetOverride ?? BudgetCalculator.GetEffectiveBudget(opt);
                opt.BudgetOverride = new RequestBudget(
                    request.HasRequestsPerMonth ? request.RequestsPerMonth : current.RequestsPerMonth,
                    request.HasRequestsPerMinute ? request.RequestsPerMinute : current.RequestsPerMinute);
            }
            else
            {
                opt.BudgetOverride = null;
            }
        });

        var budget = BudgetCalculator.Validate(snapshot);
        return Task.FromResult(Success(snapshot, budget));
    }

    internal static NjordConfig MapConfig(NjordOptions options)
    {
        var budget = BudgetCalculator.Validate(options);

        var config = new NjordConfig
        {
            ForecastDays = options.ForecastDays,
            PollIntervalSeconds = (long)options.PollInterval.TotalSeconds,
            Parameters = new ParameterConfig(),
            Enrichment = MapEnrichment(options.Enrichment),
            BudgetProjection = MapBudgetProjection(budget),
        };

        if (options.BudgetOverride is { } bo)
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

    private static DetailedEnrichmentConfig MapEnrichment(EnrichmentOptions enrichment)
    {
        return new DetailedEnrichmentConfig
        {
            Consensus = new ConsensusConfig
            {
                Enabled = enrichment.Consensus.Enabled,
                Method = enrichment.Consensus.Method,
                TrimPercent = enrichment.Consensus.TrimPercent,
            },
            Alerts = new AlertConfig
            {
                Enabled = enrichment.Alerts.Enabled,
                FrostThresholds = { enrichment.Alerts.FrostThresholds },
                HeatThresholds = { enrichment.Alerts.HeatThresholds },
                StormGustThresholds = { enrichment.Alerts.StormGustThresholds },
                HeavyRainHourlyThreshold = enrichment.Alerts.HeavyRainHourlyThreshold,
                HeavyRainDailyThreshold = enrichment.Alerts.HeavyRainDailyThreshold,
                PressureDropThreshold = enrichment.Alerts.PressureDropThreshold,
                CapeThreshold = enrichment.Alerts.CapeThreshold,
                ThunderstormPrecipThreshold = enrichment.Alerts.ThunderstormPrecipThreshold,
                ThunderstormGustThreshold = enrichment.Alerts.ThunderstormGustThreshold,
                PressureDropSevereThreshold = enrichment.Alerts.PressureDropSevereThreshold,
                FogPersistentHours = enrichment.Alerts.FogPersistentHours,
                IceThreshold = enrichment.Alerts.IceThreshold,
                WindChillThresholds = { enrichment.Alerts.WindChillThresholds },
                VisibilityThresholds = { enrichment.Alerts.VisibilityThresholds },
                TropicalNightThresholds = { enrichment.Alerts.TropicalNightThresholds },
                HumidityThresholds = { enrichment.Alerts.HumidityThresholds },
            },
            Derived = new DerivedConfig { Enabled = enrichment.Derived.Enabled },
            Trends = new TrendConfig { Enabled = enrichment.Trends.Enabled },
            Indices = new IndexConfig
            {
                Enabled = enrichment.Indices.Enabled,
                IndoorTemp = enrichment.Indices.Preferences.IndoorTemp ?? 22.0,
                IdealOutdoorTemp = enrichment.Indices.Preferences.IdealOutdoorTemp ?? 22.0,
                HeatSensitivity = enrichment.Indices.Preferences.HeatSensitivity ?? 1.0,
                HumiditySensitivity = enrichment.Indices.Preferences.HumiditySensitivity ?? 1.0,
                WindSensitivity = enrichment.Indices.Preferences.WindSensitivity ?? 1.0,
                RainSensitivity = enrichment.Indices.Preferences.RainSensitivity ?? 1.0,
                RunningIdealTempLow = enrichment.Indices.Preferences.RunningIdealTempLow ?? 5.0,
                RunningIdealTempHigh = enrichment.Indices.Preferences.RunningIdealTempHigh ?? 20.0,
                BbqMinTemp = enrichment.Indices.Preferences.BbqMinTemp ?? 10.0,
                BbqIdealWindLow = enrichment.Indices.Preferences.BbqIdealWindLow ?? 1.0,
                BbqIdealWindHigh = enrichment.Indices.Preferences.BbqIdealWindHigh ?? 3.0,
            },
            History = new HistoryConfig
            {
                Enabled = enrichment.History.Enabled,
                RetentionDays = enrichment.History.RetentionDays,
                MinSampleSize = enrichment.History.MinSampleSize,
                SnapshotInterval = enrichment.History.SnapshotInterval,
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

    private static ConfigResponse Success(NjordOptions options, BudgetValidation budget)
    {
        var response = new ConfigResponse
        {
            Applied = true,
            Config = MapConfig(options),
            BudgetProjection = MapBudgetProjection(budget),
        };
        response.Warnings.AddRange(budget.Warnings);
        return response;
    }
}
