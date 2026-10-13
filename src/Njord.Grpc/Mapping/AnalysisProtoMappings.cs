using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Messages.Pipeline;
using Njord.Grpc.V2;
using DomainAlertSeverity = Njord.Compute.Contracts.AlertSeverity;
using DomainAlertType = Njord.Compute.Contracts.AlertType;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoAlertType = Njord.Grpc.V2.AlertType;
using ProtoHorizonConsensus = Njord.Grpc.V2.HorizonConsensus;
using ProtoHorizonDerived = Njord.Grpc.V2.HorizonDerived;
using ProtoParameterConsensus = Njord.Grpc.V2.ParameterConsensus;
using ProtoParameterTrend = Njord.Grpc.V2.ParameterTrend;
using ProtoScalarDerived = Njord.Grpc.V2.ScalarDerived;
using ScoreEnvelope = Njord.Compute.Contracts.ScoreEnvelope;

namespace Njord.Grpc.Mapping;

internal static class AnalysisProtoMappings
{
    public static AlertUpdate ToProto(this AlertResult result)
    {
        var update = new AlertUpdate();
        foreach (var alert in result.Alerts)
        {
            var protoAlert = new V2.Alert
            {
                Type = MapAlertType(alert.Type),
                Severity = MapAlertSeverity(alert.Severity),
                Confidence = alert.Confidence,
                TriggerValue = alert.TriggerValue,
                Threshold = alert.Threshold,
            };
            if (alert.PeakValue is { } pv)
            {
                protoAlert.PeakValue = pv;
            }

            if (alert.HoursUntil is { } hu)
            {
                protoAlert.HoursUntil = hu;
            }

            if (alert.DurationHours is { } dh)
            {
                protoAlert.DurationHours = dh;
            }

            update.Alerts.Add(protoAlert);
        }

        return update;
    }

    public static IndexUpdate ToProto(this IndexResult result)
    {
        var update = new IndexUpdate();

        foreach (var day in result.Days ?? [])
        {
            var proto = new V2.DayScoreSet
            {
                DayOffset = day.DayOffset,
                Laundry = day.Laundry,
                Outdoor = day.Outdoor,
                Running = day.Running,
                Cycling = day.Cycling,
                Bbq = day.Bbq,
                Irrigation = day.Irrigation,
                Solar = day.Solar,
                NightVentilation = day.NightVentilation,
                HoursIncluded = day.HoursIncluded,
            };

            if (MapEnvelope(day.LaundryEnvelope) is { } le)
            {
                proto.LaundryEnvelope = le;
            }

            if (MapEnvelope(day.OutdoorEnvelope) is { } oe)
            {
                proto.OutdoorEnvelope = oe;
            }

            if (MapEnvelope(day.RunningEnvelope) is { } re)
            {
                proto.RunningEnvelope = re;
            }

            if (MapEnvelope(day.CyclingEnvelope) is { } ce)
            {
                proto.CyclingEnvelope = ce;
            }

            if (MapEnvelope(day.BbqEnvelope) is { } be)
            {
                proto.BbqEnvelope = be;
            }

            if (MapEnvelope(day.IrrigationEnvelope) is { } ie)
            {
                proto.IrrigationEnvelope = ie;
            }

            if (MapEnvelope(day.SolarEnvelope) is { } se)
            {
                proto.SolarEnvelope = se;
            }

            if (MapEnvelope(day.NightVentilationEnvelope) is { } nve)
            {
                proto.NightVentilationEnvelope = nve;
            }

            update.Days.Add(proto);
        }

        if (result.FrostProtection is { } frost)
        {
            update.Frost = new FrostInfo
            {
                HoursUntilFrost = frost.HoursUntilFrost,
                Confidence = frost.Confidence,
            };
        }

        if (result.Vpd is { } vpd)
        {
            update.Vpd = new V2.VpdInfo
            {
                Kpa = vpd.Vpd,
                Category = vpd.Category,
            };
        }

        return update;
    }

    private static V2.ScoreEnvelope? MapEnvelope(ScoreEnvelope? envelope)
    {
        if (envelope is null)
        {
            return null;
        }

        return new V2.ScoreEnvelope
        {
            Min = envelope.Min,
            Max = envelope.Max,
            Confidence = envelope.Confidence,
        };
    }

    public static TrendUpdate ToProto(this TrendResult result)
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
                Direction = trend.Direction.ToWireName(),
                Delta = trend.Delta,
            });
        }

        if (result.WeatherChange is { } wc)
        {
            update.WeatherChangeDescription = wc.ToDescription();
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
            update.StabilityLabel = stability.Stability.ToWireName();
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

    public static DerivedUpdate ToProto(this DerivedResult result)
    {
        var update = new DerivedUpdate();

        foreach (var (horizonKey, derived) in result.ByHorizon)
        {
            var proto = new ProtoHorizonDerived { Horizon = horizonKey.ToWireKey() };

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
                proto.DewpointComfort = dpc.ToWireName();
            }

            if (derived.Condition is { } wmo)
            {
                proto.WmoDescription = wmo.ToDescription();
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

    public static ConsensusUpdate ToProto(this ConsensusResult result)
    {
        var update = new ConsensusUpdate();
        update.HourlyParameters.AddRange(result.Parameters.Select(MapParameterConsensus));
        update.DailyParameters.AddRange(result.DailyParameters.Select(MapParameterConsensus));
        return update;
    }

    private static ProtoParameterConsensus MapParameterConsensus(Compute.Consensus.ParameterConsensus paramConsensus)
    {
        var proto = new ProtoParameterConsensus
        {
            Parameter = paramConsensus.Parameter.ApiName,
            Unit = paramConsensus.Parameter.Unit,
        };

        foreach (var (horizonKey, horizon) in paramConsensus.ByHorizon)
        {
            proto.ByHorizon.Add(MapHorizonConsensus(horizonKey, horizon));
        }

        return proto;
    }

    private static ProtoHorizonConsensus MapHorizonConsensus(Weather.Horizon horizonKey, Compute.Consensus.HorizonConsensus horizon)
    {
        var protoHorizon = new ProtoHorizonConsensus
        {
            Horizon = horizonKey.ToWireKey(),
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

        return protoHorizon;
    }

    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent)
    {
        return pipelineEvent switch
        {
            PipelineEvent.AlertComputed a => new EnrichmentEvent
            {
                Location = a.Result.Location.Name,
                TypeName = a.Kind.ToWireName(),
                UpdatedAt = ToTimestamp(a.Timestamp),
                Alerts = a.Result.ToProto(),
            },
            PipelineEvent.IndexComputed i => new EnrichmentEvent
            {
                Location = i.Result.Location.Name,
                TypeName = i.Kind.ToWireName(),
                UpdatedAt = ToTimestamp(i.Timestamp),
                Indices = i.Result.ToProto(),
            },
            PipelineEvent.TrendComputed t => new EnrichmentEvent
            {
                Location = t.Result.Location.Name,
                TypeName = t.Kind.ToWireName(),
                UpdatedAt = ToTimestamp(t.Timestamp),
                Trends = t.Result.ToProto(),
            },
            PipelineEvent.DerivedComputed d => new EnrichmentEvent
            {
                Location = d.Result.Location.Name,
                TypeName = d.Kind.ToWireName(),
                UpdatedAt = ToTimestamp(d.Timestamp),
                Derived = d.Result.ToProto(),
            },
            PipelineEvent.ConsensusComputed c => MapConsensusEvent(c),
            _ => null,
        };
    }

    public static GetEnrichmentsResponse ToProto(this LatestAnalysisResponse response)
    {
        var (location, analysis) = response switch
        {
            LatestAnalysisResult found => (found.Location, found.Analysis),
            LatestAnalysisNotAvailable missing => (missing.Location, null),
            _ => throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response"),
        };

        var proto = new GetEnrichmentsResponse { Location = location.Name };
        if (analysis?.ToEnrichmentEvent() is not { } evt)
        {
            return proto;
        }

        switch (evt.PayloadCase)
        {
            case EnrichmentEvent.PayloadOneofCase.Alerts: proto.Alerts = evt.Alerts; break;
            case EnrichmentEvent.PayloadOneofCase.Indices: proto.Indices = evt.Indices; break;
            case EnrichmentEvent.PayloadOneofCase.Trends: proto.Trends = evt.Trends; break;
            case EnrichmentEvent.PayloadOneofCase.Derived: proto.Derived = evt.Derived; break;
            case EnrichmentEvent.PayloadOneofCase.Consensus:
                proto.Consensus = evt.Consensus;
                proto.ConsensusUpdatedAt = evt.UpdatedAt;
                break;
        }

        return proto;
    }

    private static EnrichmentEvent MapConsensusEvent(PipelineEvent.ConsensusComputed c)
    {
        var result = new ConsensusResult(
            c.Consensus.Hourly.Parameters, c.Consensus.Daily.Parameters, c.Consensus.ComputedAt);
        return new EnrichmentEvent
        {
            Location = c.Consensus.Location.Name,
            TypeName = c.Kind.ToWireName(),
            UpdatedAt = ToTimestamp(c.Timestamp),
            Consensus = result.ToProto(),
        };
    }

    private static Google.Protobuf.WellKnownTypes.Timestamp ToTimestamp(DateTimeOffset dt) =>
        Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(dt);

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
        DomainAlertType.Ice => ProtoAlertType.Ice,
        DomainAlertType.WindChill => ProtoAlertType.WindChill,
        DomainAlertType.Visibility => ProtoAlertType.Visibility,
        DomainAlertType.TropicalNight => ProtoAlertType.TropicalNight,
        DomainAlertType.Humidity => ProtoAlertType.Humidity,
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
