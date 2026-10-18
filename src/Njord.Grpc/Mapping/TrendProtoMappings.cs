using Njord.Grpc.V2;
using Njord.Messages.Trends;
using ProtoParameterTrend = Njord.Grpc.V2.ParameterTrend;

namespace Njord.Grpc.Mapping;

internal static class TrendProtoMappings
{
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
                Direction = trend.Direction.ToProto(),
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
            update.StabilityLabel = stability.Stability.ToProto();
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
}
