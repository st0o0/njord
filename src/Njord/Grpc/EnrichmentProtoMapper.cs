using Njord.Domain.Analysis;
using Njord.Grpc.V1;

using DomainAlertType = Njord.Domain.Analysis.AlertType;
using DomainAlertSeverity = Njord.Domain.Analysis.AlertSeverity;
using ProtoAlertType = Njord.Grpc.V1.AlertType;
using ProtoAlertSeverity = Njord.Grpc.V1.AlertSeverity;
using ProtoHorizonConsensus = Njord.Grpc.V1.HorizonConsensus;
using ProtoParameterConsensus = Njord.Grpc.V1.ParameterConsensus;
using ProtoHorizonDerived = Njord.Grpc.V1.HorizonDerived;
using ProtoScalarDerived = Njord.Grpc.V1.ScalarDerived;
using ProtoParameterTrend = Njord.Grpc.V1.ParameterTrend;

namespace Njord.Grpc;

public static class EnrichmentProtoMapper
{
    public static AlertUpdate MapAlerts(AlertResult result)
    {
        var update = new AlertUpdate();
        foreach (var alert in result.Alerts)
        {
            var protoAlert = new V1.Alert
            {
                Type = MapAlertType(alert.Type),
                Severity = MapAlertSeverity(alert.Severity),
                Confidence = alert.Confidence,
                TriggerValue = alert.TriggerValue,
                Threshold = alert.Threshold,
            };
            if (alert.PeakValue is { } pv) protoAlert.PeakValue = pv;
            if (alert.HoursUntil is { } hu) protoAlert.HoursUntil = hu;
            if (alert.DurationHours is { } dh) protoAlert.DurationHours = dh;
            update.Alerts.Add(protoAlert);
        }
        return update;
    }

    public static IndexUpdate MapIndices(IndexResult result)
    {
        var update = new IndexUpdate
        {
            Laundry = result.Laundry,
            Outdoor = result.Outdoor,
            Running = result.Running,
            Cycling = result.Cycling,
            Bbq = result.Bbq,
            Irrigation = result.Irrigation,
            Solar = result.Solar,
            Ventilation = result.Ventilation,
            Hdd = result.Hdd,
            Cdd = result.Cdd,
        };

        if (result.FrostProtection is { } frost)
        {
            update.FrostHours = frost.HoursUntilFrost;
            update.FrostConfidence = frost.Confidence;
        }

        if (result.Vpd is { } vpd)
        {
            update.VpdKpa = vpd.Vpd;
            update.VpdCategory = vpd.Category;
        }

        return update;
    }

    public static TrendUpdate MapTrends(TrendResult result)
    {
        var update = new TrendUpdate();

        foreach (var (param, trend) in result.ParameterTrends)
        {
            if (trend is null)
            {
                continue;
            }

            update.ParameterTrends.Add(new ProtoParameterTrend
            {
                Parameter = param,
                Direction = trend.Direction,
                Delta = trend.Delta,
            });
        }

        if (result.WeatherChange is { } wc)
        {
            update.WeatherChangeDescription = wc.Description;
        }

        if (result.PrecipTiming.StartsInHours is { } precipStart)
        {
            update.PrecipStartsInHours = precipStart;
        }

        if (result.PrecipTiming.EndsInHours is { } precipEnd)
        {
            update.PrecipEndsInHours = precipEnd;
        }

        if (result.ExtremaTiming.MaxInHours is { } maxH)
        {
            update.TempMaxInHours = maxH;
        }

        if (result.ExtremaTiming.MinInHours is { } minH)
        {
            update.TempMinInHours = minH;
        }

        if (result.Stability is { } stability)
        {
            update.StabilityLabel = stability.Label;
            update.StabilityRatio = stability.Ratio;
        }

        if (result.Decay is { } decay)
        {
            update.DecayRate = decay.DecayRate;
            if (decay.ReliableHours is { } rh)
            {
                update.ReliableHours = rh;
            }
        }

        return update;
    }

    public static EnergyUpdate MapEnergy(EnergyResult result)
    {
        var update = new EnergyUpdate
        {
            HeatingDemand = result.HeatingDemand,
            Shading = result.Shading,
            BatteryStrategy = result.BatteryStrategy,
            NightCooling = result.NightCooling,
        };

        if (result.CopEstimate is { } cop)
        {
            update.CopEstimate = cop;
        }

        foreach (var (hoursFromNow, copValue) in result.CopOptimal)
        {
            update.CopOptimal.Add(new CopOptimalHour
            {
                HoursFromNow = hoursFromNow,
                Cop = copValue,
            });
        }

        return update;
    }

    public static DerivedUpdate MapDerived(DerivedResult result)
    {
        var update = new DerivedUpdate();

        foreach (var (horizonKey, derived) in result.ByHorizon)
        {
            var proto = new ProtoHorizonDerived { Horizon = horizonKey };

            if (derived.Beaufort is { } b)
            {
                proto.Beaufort = b;
            }

            if (derived.WindChill is { } wc)
            {
                proto.WindChill = wc;
            }

            if (derived.DewPointComfort is { } dpc)
            {
                proto.DewpointComfort = dpc;
            }

            if (derived.WmoDescription is { } wmo)
            {
                proto.WmoDescription = wmo;
            }

            update.ByHorizon.Add(proto);
        }

        var scalars = new ProtoScalarDerived();
        if (result.Scalars.DiurnalAmplitude is { } da)
        {
            scalars.DiurnalAmplitude = da;
        }

        if (result.Scalars.SunshinePct is { } sp)
        {
            scalars.SunshinePct = sp;
        }

        if (result.Scalars.Inversion is { } inv)
        {
            scalars.Inversion = inv;
        }

        update.Scalars = scalars;

        return update;
    }

    public static HistoryUpdate MapHistory(HistoryResult result)
    {
        var update = new HistoryUpdate();

        var allModels = result.Weights.Keys;
        foreach (var model in allModels)
        {
            var metrics = new ModelMetrics
            {
                Model = model.Id,
                Weight = result.Weights.GetValueOrDefault(model),
            };

            if (result.Mae7d.TryGetValue(model, out var mae7) && mae7.HasValue)
            {
                metrics.Mae7D = mae7.Value;
            }

            if (result.Mae30d.TryGetValue(model, out var mae30) && mae30.HasValue)
            {
                metrics.Mae30D = mae30.Value;
            }

            if (result.Drift.TryGetValue(model, out var drift) && drift.HasValue)
            {
                metrics.Drift = drift.Value;
            }

            update.Models.Add(metrics);
        }

        if (result.SeasonalBest is { } best)
        {
            update.SeasonalBest = best.Id;
        }

        if (result.Anomaly is { } anomaly)
        {
            update.Anomaly = anomaly.IsAnomaly;
            update.AnomalyDeviation = anomaly.DeviationSigma;
        }

        if (result.WeightedTemperature is { } wt)
        {
            update.WeightedTemperature = wt;
        }

        return update;
    }

    public static ConsensusUpdate MapConsensus(ConsensusResult result)
    {
        var update = new ConsensusUpdate();

        foreach (var paramConsensus in result.Parameters)
        {
            var proto = new ProtoParameterConsensus
            {
                Parameter = paramConsensus.Parameter.ApiName,
                Unit = paramConsensus.Parameter.Unit,
            };

            foreach (var (horizonKey, horizon) in paramConsensus.ByHorizon)
            {
                var protoHorizon = new ProtoHorizonConsensus
                {
                    Horizon = horizonKey,
                    AvailableModels = horizon.AvailableModels.Count,
                };

                if (horizon.Median is { } med)
                {
                    protoHorizon.Median = med;
                }

                if (horizon.TrimmedMean is { } tm)
                {
                    protoHorizon.TrimmedMean = tm;
                }

                if (horizon.Spread is { } s)
                {
                    protoHorizon.Spread = s;
                }

                if (horizon.Iqr is { } iqr)
                {
                    protoHorizon.Iqr = iqr;
                }

                if (horizon.Agreement is { } ag)
                {
                    protoHorizon.Agreement = ag;
                }

                proto.ByHorizon.Add(protoHorizon);
            }

            update.Parameters.Add(proto);
        }

        return update;
    }

    public static EnrichmentEvent? MapToEvent(
        string location, string typeName, object result, DateTimeOffset updatedAt)
    {
        var timestamp = updatedAt.ToUnixTimeSeconds();

        return typeName switch
        {
            "alerts" when result is AlertResult ar => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                Alerts = MapAlerts(ar),
            },
            "indices" when result is IndexResult ir => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                Indices = MapIndices(ir),
            },
            "trends" when result is TrendResult tr => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                Trends = MapTrends(tr),
            },
            "energy" when result is EnergyResult er => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                Energy = MapEnergy(er),
            },
            "derived" when result is DerivedResult dr => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                Derived = MapDerived(dr),
            },
            "history" when result is HistoryResult hr => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                History = MapHistory(hr),
            },
            "consensus" when result is ConsensusResult cr => new EnrichmentEvent
            {
                Location = location,
                TypeName = typeName,
                UpdatedAt = timestamp,
                Consensus = MapConsensus(cr),
            },
            _ => null,
        };
    }

    private static ProtoAlertType MapAlertType(DomainAlertType type) => type switch
    {
        DomainAlertType.Frost => ProtoAlertType.Frost,
        DomainAlertType.Heat => ProtoAlertType.Heat,
        DomainAlertType.Storm => ProtoAlertType.Storm,
        DomainAlertType.HeavyRain => ProtoAlertType.HeavyRain,
        DomainAlertType.Uv => ProtoAlertType.Uv,
        DomainAlertType.Fog => ProtoAlertType.Fog,
        DomainAlertType.Snow => ProtoAlertType.Snow,
        DomainAlertType.PressureDrop => ProtoAlertType.PressureDrop,
        DomainAlertType.Thunderstorm => ProtoAlertType.Thunderstorm,
        _ => ProtoAlertType.Unspecified,
    };

    private static ProtoAlertSeverity MapAlertSeverity(DomainAlertSeverity severity) => severity switch
    {
        DomainAlertSeverity.None => ProtoAlertSeverity.None,
        DomainAlertSeverity.Yellow => ProtoAlertSeverity.Yellow,
        DomainAlertSeverity.Orange => ProtoAlertSeverity.Orange,
        DomainAlertSeverity.Red => ProtoAlertSeverity.Red,
        _ => ProtoAlertSeverity.None,
    };
}
