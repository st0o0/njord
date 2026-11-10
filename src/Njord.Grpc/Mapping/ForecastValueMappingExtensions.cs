using Njord.Messages.Results;
using Njord.Messages.Results.Forecasts;

namespace Njord.Grpc.Mapping;

internal static class ForecastValueMappingExtensions
{
    public static Proto.ForecastValue ToForecastValue(this ForecastFeatureResult r)
    {
        var proto = new Proto.ForecastValue();
        PopulateExtra(proto, r);
        return proto;
    }

    private static void PopulateExtra(Proto.ForecastValue proto, ForecastFeatureResult r)
    {
        switch (r)
        {
            case GrowingDegreeResult gd:
                proto.Extra["cumulative_gdd"] = gd.CumulativeGdd;
                foreach (var day in gd.Days)
                {
                    proto.Extra[$"gdd_{day.Date:yyyy_MM_dd}"] = day.Gdd;
                }

                break;
            case SeasonalOutlookResult so:
                foreach (var month in so.Months)
                {
                    var key = month.Month.ToString("yyyy_MM");
                    proto.Extra[$"temp_anomaly_{key}"] = month.TemperatureAnomaly;
                    proto.Extra[$"precip_anomaly_{key}"] = month.PrecipitationAnomalyPercent;
                    proto.Extra[$"confidence_{key}"] = (double)month.Confidence;
                }

                break;
            case ForecastModelComparisonResult fmc:
                PopulateModelComparison(proto, fmc.Days);
                break;
            case EnsembleModelComparisonResult emc:
                PopulateModelComparison(proto, emc.Days);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(r), r.GetType().Name, "Unknown forecast result type");
        }
    }

    private static void PopulateModelComparison(Proto.ForecastValue proto, System.Collections.Immutable.ImmutableArray<ModelComparisonDay> days)
    {
        foreach (var day in days)
        {
            foreach (var model in day.Models)
            {
                var prefix = $"{model.ModelId}_{day.Date:yyyy_MM_dd}";
                if (model.MaxTemperature is { } max)
                {
                    proto.Extra[$"{prefix}_max"] = max;
                }

                if (model.MinTemperature is { } min)
                {
                    proto.Extra[$"{prefix}_min"] = min;
                }

                if (model.Precipitation is { } precip)
                {
                    proto.Extra[$"{prefix}_precip"] = precip;
                }
            }
        }
    }
}
