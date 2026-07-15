using System.Diagnostics;
using System.Reflection;
using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc.V1;
using Njord.Pipeline;

namespace Njord.Grpc;

public sealed class ConfigGrpcService : V1.ConfigService.ConfigServiceBase
{
    private static readonly string Version =
        typeof(ConfigGrpcService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private static readonly DateTimeOffset ProcessStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

    private readonly IOptionsMonitor<NjordOptions> _optionsMonitor;
    private readonly ConfigPersistence _persistence;
    private readonly BudgetTracker _budgetTracker;
    private readonly ActorRegistry _actorRegistry;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public ConfigGrpcService(
        IOptionsMonitor<NjordOptions> optionsMonitor,
        ConfigPersistence persistence,
        BudgetTracker budgetTracker,
        ActorRegistry actorRegistry)
    {
        _optionsMonitor = optionsMonitor;
        _persistence = persistence;
        _budgetTracker = budgetTracker;
        _actorRegistry = actorRegistry;
    }

    // ═══════════════════════════════════════
    // Read RPCs
    // ═══════════════════════════════════════

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

    public override Task<ServerStatus> GetStatus(GetStatusRequest request, ServerCallContext context)
    {
        var uptime = DateTimeOffset.UtcNow - ProcessStart;
        var budget = _optionsMonitor.CurrentValue.EffectiveBudget;
        var (monthlyUsed, dailyUsed) = _budgetTracker.GetUsage();

        var status = new ServerStatus
        {
            Version = Version,
            UptimeSeconds = (long)uptime.TotalSeconds,
            Budget = new BudgetStatus
            {
                MonthlyLimit = budget.RequestsPerMonth,
                MonthlyUsed = monthlyUsed,
                DailyLimit = 10_000, // Open-Meteo free-tier daily soft limit
                DailyUsed = dailyUsed,
                UsagePercent = budget.RequestsPerMonth > 0
                    ? (double)monthlyUsed / budget.RequestsPerMonth * 100
                    : 0,
            },
            // TODO: Query SchedulerActor for per-model status once status queries are implemented
        };

        return Task.FromResult(status);
    }

    // ═══════════════════════════════════════
    // Operations RPCs
    // ═══════════════════════════════════════

    public override async Task<TriggerPollResponse> TriggerPoll(TriggerPollRequest request, ServerCallContext context)
    {
        var scheduler = _actorRegistry.Get<SchedulerActor>();
        var result = await scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll(request.Location, request.Model), AskTimeout);

        var response = new TriggerPollResponse { TriggeredCount = result.Count };
        response.Targets.AddRange(result.Targets);
        return response;
    }

    // ═══════════════════════════════════════
    // Mutation RPCs
    // ═══════════════════════════════════════

    public override async Task<ConfigResponse> AddLocation(AddLocationRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Rejected("Location name is required");
            }

            if (options.Locations.Any(l => l.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Rejected("Location already exists");
            }

            options.Locations.Add(new LocationOptions
            {
                Name = request.Name,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Models = request.Models.Count > 0 ? [.. request.Models] : null,
            });

            var budget = BudgetValidator.Validate(options);
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

    public override async Task<ConfigResponse> RemoveLocation(RemoveLocationRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            var existing = options.Locations
                .FirstOrDefault(l => l.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                return Rejected("Location not found");
            }

            if (options.Locations.Count == 1 && !request.Force)
            {
                return Rejected("Cannot remove the last location without force=true");
            }

            options.Locations.Remove(existing);

            var budget = BudgetValidator.Validate(options);
            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public override async Task<ConfigResponse> UpdateLocation(UpdateLocationRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            var existing = options.Locations
                .FirstOrDefault(l => l.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                return Rejected("Location not found");
            }

            if (request.HasLatitude)
            {
                existing.Latitude = request.Latitude;
            }

            if (request.HasLongitude)
            {
                existing.Longitude = request.Longitude;
            }

            if (request.Models.Count > 0)
            {
                existing.Models = [.. request.Models];
            }

            var budget = BudgetValidator.Validate(options);
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

    public override async Task<ConfigResponse> UpdateForecastSettings(
        UpdateForecastSettingsRequest request, ServerCallContext context)
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

            var budget = BudgetValidator.Validate(options);
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

    public override async Task<ConfigResponse> UpdateEnrichmentConfig(
        UpdateEnrichmentConfigRequest request, ServerCallContext context)
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
                {
                    options.Enrichment.Indices.Enabled = indices.Enabled;
                }

                if (indices.HasHeatingBaseTemp)
                {
                    options.Enrichment.Indices.HeatingBaseTemp = indices.HeatingBaseTemp;
                }

                if (indices.HasCoolingBaseTemp)
                {
                    options.Enrichment.Indices.CoolingBaseTemp = indices.CoolingBaseTemp;
                }

                if (indices.HasIndoorTemp)
                {
                    options.Enrichment.Indices.IndoorTemp = indices.IndoorTemp;
                }
            }

            if (request.Energy is { } energy)
            {
                if (energy.HasEnabled)
                {
                    options.Enrichment.Energy.Enabled = energy.Enabled;
                }

                if (energy.HasFlowTemp)
                {
                    options.Enrichment.Energy.FlowTemp = energy.FlowTemp;
                }

                if (energy.HasCarnotEfficiency)
                {
                    options.Enrichment.Energy.CarnotEfficiency = energy.CarnotEfficiency;
                }

                if (energy.HasHeatingBaseTemp)
                {
                    options.Enrichment.Energy.HeatingBaseTemp = energy.HeatingBaseTemp;
                }

                if (energy.HasCopOptimalHours)
                {
                    options.Enrichment.Energy.CopOptimalHours = energy.CopOptimalHours;
                }

                if (energy.HasIndoorTemp)
                {
                    options.Enrichment.Energy.IndoorTemp = energy.IndoorTemp;
                }
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

            var budget = BudgetValidator.Validate(options);
            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public override async Task<ConfigResponse> UpdateBudget(UpdateBudgetRequest request, ServerCallContext context)
    {
        await _mutationLock.WaitAsync(context.CancellationToken);
        try
        {
            var options = CloneOptions(_optionsMonitor.CurrentValue);

            if (request.HasRequestsPerMonth || request.HasRequestsPerMinute)
            {
                var current = options.BudgetOverride ?? options.EffectiveBudget;
                options.BudgetOverride = new RequestBudget(
                    request.HasRequestsPerMonth ? request.RequestsPerMonth : current.RequestsPerMonth,
                    request.HasRequestsPerMinute ? request.RequestsPerMinute : current.RequestsPerMinute);
            }
            else
            {
                // Clear override, revert to free-tier defaults
                options.BudgetOverride = null;
            }

            var budget = BudgetValidator.Validate(options);
            await _persistence.SaveAsync(options);
            return Success(options, budget);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    // ═══════════════════════════════════════
    // Mapping helpers
    // ═══════════════════════════════════════

    internal static NjordConfig MapConfig(NjordOptions options)
    {
        var budget = BudgetValidator.Validate(options);

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
            var locationConfig = new LocationConfig
            {
                Name = loc.Name,
                Latitude = loc.Latitude,
                Longitude = loc.Longitude,
            };
            locationConfig.Models.AddRange(loc.ResolveModels(options.Models));
            config.Locations.Add(locationConfig);
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
            Derived = new DerivedConfig { Enabled = enrichment.Derived.Enabled },
            Trends = new TrendConfig { Enabled = enrichment.Trends.Enabled },
            Indices = new IndexConfig
            {
                Enabled = enrichment.Indices.Enabled,
                HeatingBaseTemp = enrichment.Indices.HeatingBaseTemp,
                CoolingBaseTemp = enrichment.Indices.CoolingBaseTemp,
                IndoorTemp = enrichment.Indices.IndoorTemp,
            },
            Energy = new EnergyConfig
            {
                Enabled = enrichment.Energy.Enabled,
                FlowTemp = enrichment.Energy.FlowTemp,
                CarnotEfficiency = enrichment.Energy.CarnotEfficiency,
                HeatingBaseTemp = enrichment.Energy.HeatingBaseTemp,
                CopOptimalHours = enrichment.Energy.CopOptimalHours,
                IndoorTemp = enrichment.Energy.IndoorTemp,
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

    // ═══════════════════════════════════════
    // Mutation helpers
    // ═══════════════════════════════════════

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
                Alerts = new AlertThresholdOptions
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
                    HeatingBaseTemp = source.Enrichment.Indices.HeatingBaseTemp,
                    CoolingBaseTemp = source.Enrichment.Indices.CoolingBaseTemp,
                    IndoorTemp = source.Enrichment.Indices.IndoorTemp,
                },
                Energy = new EnergyOptions
                {
                    Enabled = source.Enrichment.Energy.Enabled,
                    FlowTemp = source.Enrichment.Energy.FlowTemp,
                    CarnotEfficiency = source.Enrichment.Energy.CarnotEfficiency,
                    HeatingBaseTemp = source.Enrichment.Energy.HeatingBaseTemp,
                    CopOptimalHours = source.Enrichment.Energy.CopOptimalHours,
                    IndoorTemp = source.Enrichment.Energy.IndoorTemp,
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
