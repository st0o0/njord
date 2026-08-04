using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc.V2;

namespace Njord.Grpc;

public sealed class AdminGrpcService(
    IOptionsMonitor<NjordOptions> optionsMonitor,
    ConfigPersistence persistence,
    ILogger<AdminGrpcService> logger) : V2.AdminService.AdminServiceBase
{
    private readonly IOptionsMonitor<NjordOptions> _optionsMonitor = optionsMonitor;
    private readonly ConfigPersistence _persistence = persistence;
    private readonly ILogger<AdminGrpcService> _logger = logger;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public override Task<NjordConfig> GetConfig(GetConfigRequest request, ServerCallContext context)
    {
        return Task.FromResult(MapConfig(_optionsMonitor.CurrentValue));
    }

    public override async Task StreamConfig(
        StreamConfigRequest request,
        IServerStreamWriter<NjordConfig> responseStream,
        ServerCallContext context)
    {
        await responseStream.WriteAsync(MapConfig(_optionsMonitor.CurrentValue));

        var tcs = new TaskCompletionSource();
        using var registration = context.CancellationToken.Register(() => tcs.TrySetResult());

        using var onChange = _optionsMonitor.OnChange(async (options, _) =>
        {
            if (!context.CancellationToken.IsCancellationRequested)
            {
                await responseStream.WriteAsync(MapConfig(options));
            }
        });

        await tcs.Task;
    }

    public override async Task<ConfigResponse> SetLocations(SetLocationsRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            if (request.Locations.Count == 0)
            {
                return Rejected("Cannot set empty location list");
            }

            options.Locations = request.Locations.Select(l => new LocationOptions
            {
                Name = l.Name,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Models = l.Models.Count > 0 ? [.. l.Models] : null,
            }).ToList();

            var budget = BudgetCalculator.Validate(options);
            if (!budget.WithinBudget)
            {
                return Rejected($"Would exceed budget: {budget.UsagePercent:F0}% of monthly limit");
            }

            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public override async Task<ConfigResponse> SetSettings(SetSettingsRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            if (request.HasPollIntervalSeconds)
            {
                if (request.PollIntervalSeconds < 60)
                {
                    return Rejected("Poll interval must be at least 60 seconds");
                }

                options.PollInterval = TimeSpan.FromSeconds(request.PollIntervalSeconds);
            }

            if (request.HasForecastDays)
            {
                if (request.ForecastDays is < 1 or > 16)
                {
                    return Rejected("Forecast days must be between 1 and 16");
                }

                options.ForecastDays = request.ForecastDays;
            }

            if (request.Horizons.Count > 0)
            {
                options.Horizons = [.. request.Horizons];
            }

            if (request.DefaultModels.Count > 0)
            {
                options.Models = [.. request.DefaultModels];
            }

            if (request.Parameters is not null)
            {
                options.Parameters = new ParameterOptions
                {
                    Groups = [.. request.Parameters.Groups],
                    Extra = [.. request.Parameters.Extra],
                    Exclude = [.. request.Parameters.Exclude],
                };
            }

            var budget = BudgetCalculator.Validate(options);
            if (!budget.WithinBudget)
            {
                return Rejected($"Would exceed budget: {budget.UsagePercent:F0}% of monthly limit");
            }

            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public override async Task<ConfigResponse> SetEnrichment(SetEnrichmentRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            if (request.Consensus is { } consensus)
            {
                if (consensus.HasEnabled)
                {
                    options.Enrichment.Consensus.Enabled = consensus.Enabled;
                }

                if (consensus.HasMethod)
                {
                    options.Enrichment.Consensus.Method = consensus.Method;
                }

                if (consensus.HasTrimPercent)
                {
                    options.Enrichment.Consensus.TrimPercent = consensus.TrimPercent;
                }
            }

            if (request.Alerts is { } alerts)
            {
                if (alerts.HasEnabled)
                {
                    options.Enrichment.Alerts.Enabled = alerts.Enabled;
                }

                if (alerts.HasFrostThreshold)
                {
                    options.Enrichment.Alerts.FrostThreshold = alerts.FrostThreshold;
                }

                if (alerts.HeatThresholds.Count > 0)
                {
                    options.Enrichment.Alerts.HeatThresholds = [.. alerts.HeatThresholds];
                }

                if (alerts.HasStormGustThreshold)
                {
                    options.Enrichment.Alerts.StormGustThreshold = alerts.StormGustThreshold;
                }

                if (alerts.HasHeavyRainHourlyThreshold)
                {
                    options.Enrichment.Alerts.HeavyRainHourlyThreshold = alerts.HeavyRainHourlyThreshold;
                }

                if (alerts.HasHeavyRainDailyThreshold)
                {
                    options.Enrichment.Alerts.HeavyRainDailyThreshold = alerts.HeavyRainDailyThreshold;
                }

                if (alerts.HasPressureDropThreshold)
                {
                    options.Enrichment.Alerts.PressureDropThreshold = alerts.PressureDropThreshold;
                }

                if (alerts.HasCapeThreshold)
                {
                    options.Enrichment.Alerts.CapeThreshold = alerts.CapeThreshold;
                }

                if (alerts.HasThunderstormPrecipThreshold)
                {
                    options.Enrichment.Alerts.ThunderstormPrecipThreshold = alerts.ThunderstormPrecipThreshold;
                }

                if (alerts.HasThunderstormGustThreshold)
                {
                    options.Enrichment.Alerts.ThunderstormGustThreshold = alerts.ThunderstormGustThreshold;
                }
            }

            if (request.Derived is { } derived)
            {
                if (derived.HasEnabled)
                {
                    options.Enrichment.Derived.Enabled = derived.Enabled;
                }
            }

            if (request.Trends is { } trends)
            {
                if (trends.HasEnabled)
                {
                    options.Enrichment.Trends.Enabled = trends.Enabled;
                }
            }

            if (request.Indices is { } indices)
            {
                if (indices.HasEnabled)
                    options.Enrichment.Indices.Enabled = indices.Enabled;
                if (indices.HasIndoorTemp)
                    options.Enrichment.Indices.Preferences.IndoorTemp = indices.IndoorTemp;
                if (indices.HasIdealOutdoorTemp)
                    options.Enrichment.Indices.Preferences.IdealOutdoorTemp = indices.IdealOutdoorTemp;
                if (indices.HasHeatSensitivity)
                    options.Enrichment.Indices.Preferences.HeatSensitivity = indices.HeatSensitivity;
                if (indices.HasHumiditySensitivity)
                    options.Enrichment.Indices.Preferences.HumiditySensitivity = indices.HumiditySensitivity;
                if (indices.HasWindSensitivity)
                    options.Enrichment.Indices.Preferences.WindSensitivity = indices.WindSensitivity;
                if (indices.HasRainSensitivity)
                    options.Enrichment.Indices.Preferences.RainSensitivity = indices.RainSensitivity;
                if (indices.HasRunningIdealTempLow)
                    options.Enrichment.Indices.Preferences.RunningIdealTempLow = indices.RunningIdealTempLow;
                if (indices.HasRunningIdealTempHigh)
                    options.Enrichment.Indices.Preferences.RunningIdealTempHigh = indices.RunningIdealTempHigh;
                if (indices.HasBbqMinTemp)
                    options.Enrichment.Indices.Preferences.BbqMinTemp = indices.BbqMinTemp;
                if (indices.HasBbqIdealWindLow)
                    options.Enrichment.Indices.Preferences.BbqIdealWindLow = indices.BbqIdealWindLow;
                if (indices.HasBbqIdealWindHigh)
                    options.Enrichment.Indices.Preferences.BbqIdealWindHigh = indices.BbqIdealWindHigh;
            }

            if (request.History is { } history)
            {
                if (history.HasEnabled)
                {
                    options.Enrichment.History.Enabled = history.Enabled;
                }

                if (history.HasRetentionDays)
                {
                    options.Enrichment.History.RetentionDays = history.RetentionDays;
                }

                if (history.HasMinSampleSize)
                {
                    options.Enrichment.History.MinSampleSize = history.MinSampleSize;
                }

                if (history.HasSnapshotInterval)
                {
                    options.Enrichment.History.SnapshotInterval = history.SnapshotInterval;
                }
            }

            var budget = BudgetCalculator.Validate(options);
            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public override async Task<ConfigResponse> SetBudget(SetBudgetRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            if (request.HasRequestsPerMonth || request.HasRequestsPerMinute)
            {
                var current = options.BudgetOverride ?? BudgetCalculator.GetEffectiveBudget(options);
                options.BudgetOverride = new RequestBudget(
                    request.HasRequestsPerMonth ? request.RequestsPerMonth : current.RequestsPerMonth,
                    request.HasRequestsPerMinute ? request.RequestsPerMinute : current.RequestsPerMinute);
            }
            else
            {
                options.BudgetOverride = null;
            }

            var budget = BudgetCalculator.Validate(options);
            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    internal static NjordConfig MapConfig(NjordOptions options)
    {
        var budget = BudgetCalculator.Validate(options);

        var config = new NjordConfig
        {
            ForecastDays = options.ForecastDays,
            PollIntervalSeconds = (long)options.PollInterval.TotalSeconds,
            Parameters = new V2.ParameterConfig(),
            Enrichment = MapEnrichment(options.Enrichment),
            BudgetProjection = MapBudgetProjection(budget),
        };

        if (options.BudgetOverride is { } bo)
        {
            config.BudgetOverride = new V2.BudgetConfig
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
            Consensus = new V2.ConsensusConfig
            {
                Enabled = enrichment.Consensus.Enabled,
                Method = enrichment.Consensus.Method,
                TrimPercent = enrichment.Consensus.TrimPercent,
            },
            Alerts = new V2.AlertConfig
            {
                Enabled = enrichment.Alerts.Enabled,
                FrostThreshold = enrichment.Alerts.FrostThreshold,
                HeatThresholds = { enrichment.Alerts.HeatThresholds },
                StormGustThreshold = enrichment.Alerts.StormGustThreshold,
                HeavyRainHourlyThreshold = enrichment.Alerts.HeavyRainHourlyThreshold,
                HeavyRainDailyThreshold = enrichment.Alerts.HeavyRainDailyThreshold,
                PressureDropThreshold = enrichment.Alerts.PressureDropThreshold,
                CapeThreshold = enrichment.Alerts.CapeThreshold,
                ThunderstormPrecipThreshold = enrichment.Alerts.ThunderstormPrecipThreshold,
                ThunderstormGustThreshold = enrichment.Alerts.ThunderstormGustThreshold,
            },
            Derived = new V2.DerivedConfig { Enabled = enrichment.Derived.Enabled },
            Trends = new V2.TrendConfig { Enabled = enrichment.Trends.Enabled },
            Indices = new V2.IndexConfig
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
            History = new V2.HistoryConfig
            {
                Enabled = enrichment.History.Enabled,
                RetentionDays = enrichment.History.RetentionDays,
                MinSampleSize = enrichment.History.MinSampleSize,
                SnapshotInterval = enrichment.History.SnapshotInterval,
            },
        };
    }

    private static V2.BudgetProjection MapBudgetProjection(BudgetValidation validation)
    {
        return new V2.BudgetProjection
        {
            ProjectedMonthlyCalls = validation.ProjectedMonthlyCalls,
            MonthlyLimit = validation.MonthlyLimit,
            UsagePercent = validation.UsagePercent,
            WithinBudget = validation.WithinBudget,
        };
    }

    internal static NjordOptions CloneOptions(NjordOptions source)
    {
        return new NjordOptions
        {
            PollInterval = source.PollInterval,
            Locations = source.Locations.Select(l => new LocationOptions
            {
                Name = l.Name,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Models = l.Models is not null ? [.. l.Models] : null,
            }).ToList(),
            Models = [.. source.Models],
            Horizons = [.. source.Horizons],
            ForecastDays = source.ForecastDays,
            Parameters = new ParameterOptions
            {
                Groups = [.. source.Parameters.Groups],
                Extra = [.. source.Parameters.Extra],
                Exclude = [.. source.Parameters.Exclude],
            },
            BudgetOverride = source.BudgetOverride,
            Enrichment = new EnrichmentOptions
            {
                Consensus = new ConsensusOptions
                {
                    Enabled = source.Enrichment.Consensus.Enabled,
                    Method = source.Enrichment.Consensus.Method,
                    TrimPercent = source.Enrichment.Consensus.TrimPercent,
                },
                Alerts = new AlertOptions
                {
                    Enabled = source.Enrichment.Alerts.Enabled,
                    FrostThreshold = source.Enrichment.Alerts.FrostThreshold,
                    HeatThresholds = [.. source.Enrichment.Alerts.HeatThresholds],
                    StormGustThreshold = source.Enrichment.Alerts.StormGustThreshold,
                    HeavyRainHourlyThreshold = source.Enrichment.Alerts.HeavyRainHourlyThreshold,
                    HeavyRainDailyThreshold = source.Enrichment.Alerts.HeavyRainDailyThreshold,
                    PressureDropThreshold = source.Enrichment.Alerts.PressureDropThreshold,
                    CapeThreshold = source.Enrichment.Alerts.CapeThreshold,
                    ThunderstormPrecipThreshold = source.Enrichment.Alerts.ThunderstormPrecipThreshold,
                    ThunderstormGustThreshold = source.Enrichment.Alerts.ThunderstormGustThreshold,
                },
                Derived = new DerivedOptions { Enabled = source.Enrichment.Derived.Enabled },
                Trends = new TrendOptions { Enabled = source.Enrichment.Trends.Enabled },
                Indices = new IndexOptions
                {
                    Enabled = source.Enrichment.Indices.Enabled,
                    Preferences = new IndexPreferences
                    {
                        IndoorTemp = source.Enrichment.Indices.Preferences.IndoorTemp,
                        IdealOutdoorTemp = source.Enrichment.Indices.Preferences.IdealOutdoorTemp,
                        HeatSensitivity = source.Enrichment.Indices.Preferences.HeatSensitivity,
                        HumiditySensitivity = source.Enrichment.Indices.Preferences.HumiditySensitivity,
                        WindSensitivity = source.Enrichment.Indices.Preferences.WindSensitivity,
                        RainSensitivity = source.Enrichment.Indices.Preferences.RainSensitivity,
                        RunningIdealTempLow = source.Enrichment.Indices.Preferences.RunningIdealTempLow,
                        RunningIdealTempHigh = source.Enrichment.Indices.Preferences.RunningIdealTempHigh,
                        BbqMinTemp = source.Enrichment.Indices.Preferences.BbqMinTemp,
                        BbqIdealWindLow = source.Enrichment.Indices.Preferences.BbqIdealWindLow,
                        BbqIdealWindHigh = source.Enrichment.Indices.Preferences.BbqIdealWindHigh,
                    },
                },
                History = new HistoryOptions
                {
                    Enabled = source.Enrichment.History.Enabled,
                    RetentionDays = source.Enrichment.History.RetentionDays,
                    MinSampleSize = source.Enrichment.History.MinSampleSize,
                    SnapshotInterval = source.Enrichment.History.SnapshotInterval,
                },
            },
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
