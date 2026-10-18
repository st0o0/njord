using Njord.Grpc.V2;
using Njord.Messages.Indices;
using ScoreEnvelope = Njord.Messages.Indices.ScoreEnvelope;

namespace Njord.Grpc.Mapping;

internal static class IndexProtoMappings
{
    public static IndexUpdate ToProto(this IndexResult result)
    {
        var update = new IndexUpdate();

        foreach (var day in result.Days ?? [])
        {
            var proto = new V2.DayScoreSet
            {
                DayOffset = day.DayOffset,
                HoursIncluded = day.HoursIncluded,
            };

            foreach (var (kind, scored) in day.Scores)
            {
                SetScore(proto, kind, scored);
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
                Category = vpd.Category.ToProto(),
            };
        }

        return update;
    }

    private static void SetScore(V2.DayScoreSet proto, IndexKind kind, ScoredIndex scored)
    {
        switch (kind)
        {
            case IndexKind.Laundry:
                proto.Laundry = scored.Value;
                if (scored.Envelope.ToProto() is { } le) proto.LaundryEnvelope = le;
                break;
            case IndexKind.Outdoor:
                proto.Outdoor = scored.Value;
                if (scored.Envelope.ToProto() is { } oe) proto.OutdoorEnvelope = oe;
                break;
            case IndexKind.Running:
                proto.Running = scored.Value;
                if (scored.Envelope.ToProto() is { } re) proto.RunningEnvelope = re;
                break;
            case IndexKind.Cycling:
                proto.Cycling = scored.Value;
                if (scored.Envelope.ToProto() is { } ce) proto.CyclingEnvelope = ce;
                break;
            case IndexKind.Bbq:
                proto.Bbq = scored.Value;
                if (scored.Envelope.ToProto() is { } be) proto.BbqEnvelope = be;
                break;
            case IndexKind.Irrigation:
                proto.Irrigation = scored.Value;
                if (scored.Envelope.ToProto() is { } ie) proto.IrrigationEnvelope = ie;
                break;
            case IndexKind.Solar:
                proto.Solar = scored.Value;
                if (scored.Envelope.ToProto() is { } se) proto.SolarEnvelope = se;
                break;
            case IndexKind.NightVentilation:
                proto.NightVentilation = scored.Value;
                if (scored.Envelope.ToProto() is { } nve) proto.NightVentilationEnvelope = nve;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static V2.ScoreEnvelope? ToProto(this ScoreEnvelope? envelope)
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
}
