using Njord.Messages.Results;
using Njord.Messages.Results.Trends;

namespace Njord.Grpc.Mapping;

internal static class TrendValueMappingExtensions
{
    public static Proto.TrendValue ToTrendValue(this TrendFeatureResult r)
    {
        var proto = new Proto.TrendValue();
        PopulateExtra(proto, r);
        return proto;
    }

    private static void PopulateExtra(Proto.TrendValue proto, TrendFeatureResult r)
    {
        switch (r)
        {
            case ConfidenceTrendResult ct:
                proto.Extra["direction"] = (double)ct.Direction;
                proto.Extra["magnitude"] = ct.Magnitude;
                proto.Extra["current_avg_std_dev"] = ct.CurrentAvgStdDev;
                proto.Extra["previous_avg_std_dev"] = ct.PreviousAvgStdDev;
                break;
            case PollenTrendResult pt:
                if (pt.PeakType is { } peakType)
                {
                    proto.Extra["peak_type"] = (double)peakType;
                }

                if (pt.PeakTime is { } peakTime)
                {
                    proto.Extra["peak_time"] = peakTime.ToUnixTimeSeconds();
                }

                if (pt.Birch is { } birch)
                {
                    proto.Extra["birch"] = (double)birch;
                }

                if (pt.Grass is { } grass)
                {
                    proto.Extra["grass"] = (double)grass;
                }

                if (pt.Alder is { } alder)
                {
                    proto.Extra["alder"] = (double)alder;
                }

                if (pt.Mugwort is { } mugwort)
                {
                    proto.Extra["mugwort"] = (double)mugwort;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(r), r.GetType().Name, "Unknown trend result type");
        }
    }
}
