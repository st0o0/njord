using System.Collections.Immutable;
using Njord.Core;
using Njord.Messages.Alerts;

namespace Njord.Messages.Results.Forecasts;

public sealed record SeasonalMonth(DateOnly Month, double TemperatureAnomaly, double PrecipitationAnomalyPercent, ConfidenceLevel Confidence);

public sealed record SeasonalOutlookResult(Location Location, ImmutableArray<SeasonalMonth> Months) : ForecastFeatureResult(Location);

public sealed record ModelDayValues(string ModelId, double? MaxTemperature, double? MinTemperature, double? Precipitation);

public sealed record ModelComparisonDay(DateOnly Date, ImmutableArray<ModelDayValues> Models);

public sealed record ModelComparisonResult(Location Location, ImmutableArray<ModelComparisonDay> Days) : ForecastFeatureResult(Location);

public sealed record GrowingDegreeDay(DateOnly Date, double Gdd);

public sealed record GrowingDegreeResult(Location Location, ImmutableArray<GrowingDegreeDay> Days, double CumulativeGdd) : ForecastFeatureResult(Location);
