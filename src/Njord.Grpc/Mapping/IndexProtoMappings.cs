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

            if (day.LaundryEnvelope.ToProto() is { } le)
            {
                proto.LaundryEnvelope = le;
            }

            if (day.OutdoorEnvelope.ToProto() is { } oe)
            {
                proto.OutdoorEnvelope = oe;
            }

            if (day.RunningEnvelope.ToProto() is { } re)
            {
                proto.RunningEnvelope = re;
            }

            if (day.CyclingEnvelope.ToProto() is { } ce)
            {
                proto.CyclingEnvelope = ce;
            }

            if (day.BbqEnvelope.ToProto() is { } be)
            {
                proto.BbqEnvelope = be;
            }

            if (day.IrrigationEnvelope.ToProto() is { } ie)
            {
                proto.IrrigationEnvelope = ie;
            }

            if (day.SolarEnvelope.ToProto() is { } se)
            {
                proto.SolarEnvelope = se;
            }

            if (day.NightVentilationEnvelope.ToProto() is { } nve)
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
                Category = vpd.Category.ToProto(),
            };
        }

        return update;
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
