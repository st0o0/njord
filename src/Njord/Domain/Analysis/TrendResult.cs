using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record ParameterTrend(string Direction, double Delta);

public sealed record TrendResult(
    string Location,
    IReadOnlyDictionary<string, ParameterTrend?> ParameterTrends,
    WeatherChangeResult? WeatherChange,
    (int? StartsInHours, int? EndsInHours) PrecipTiming,
    (int? MaxInHours, int? MinInHours) ExtremaTiming,
    (string Label, double Ratio)? Stability,
    (double DecayRate, int? ReliableHours)? Decay)
{
    private static readonly ParameterDef[] TrendParamDefs =
    [
        ParameterRegistry.Temperature2m,
        ParameterRegistry.WindSpeed10m,
        ParameterRegistry.Precipitation,
        ParameterRegistry.CloudCover,
    ];
    private static readonly Dictionary<ParameterDef, double> Thresholds = new()
    {
        [ParameterRegistry.Temperature2m] = 0.5,
        [ParameterRegistry.WindSpeed10m] = 0.5,
        [ParameterRegistry.Precipitation] = 0.5,
        [ParameterRegistry.CloudCover] = 5.0,
    };

    public static TrendResult Compute(
        ModelSnapshot current,
        ModelSnapshot? previous,
        string location,
        IReadOnlyList<int> horizons,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        var tempParam = parameters.Get(ParameterRegistry.Temperature2m);
        var precipParam = parameters.Get(ParameterRegistry.Precipitation);
        var weatherCodeParam = parameters.Get(ParameterRegistry.WeatherCode);

        var paramTrends = new Dictionary<string, ParameterTrend?>();
        WeatherChangeResult? weatherChange = null;
        (string Label, double Ratio)? stability = null;

        var referenceHorizon = horizons.Count > 0 ? horizons[0] : 3;

        foreach (var paramDef in TrendParamDefs)
        {
            var param = parameters.Get(paramDef);
            if (param is null || previous is null)
            {
                paramTrends[paramDef.ApiName] = null;
                continue;
            }

            var currMedian = MedianAtHorizon(current, param, location, referenceHorizon, now);
            var prevMedian = MedianAtHorizon(previous, param, location, referenceHorizon, now);
            var threshold = Thresholds.GetValueOrDefault(paramDef, 0.5);

            var trend = TrendAnalyzer.TrendDirection(prevMedian, currMedian, threshold);
            paramTrends[paramDef.ApiName] = trend is { } t ? new ParameterTrend(t.Direction, t.Delta) : null;
        }

        if (previous is not null && weatherCodeParam is not null)
        {
            var currCode = MedianAtHorizon(current, weatherCodeParam, location, referenceHorizon, now);
            var prevCode = MedianAtHorizon(previous, weatherCodeParam, location, referenceHorizon, now);
            weatherChange = TrendAnalyzer.WeatherChange(
                prevCode.HasValue ? (int)Math.Round(prevCode.Value) : null,
                currCode.HasValue ? (int)Math.Round(currCode.Value) : null);
        }

        var precipTiming = (StartsInHours: (int?)null, EndsInHours: (int?)null);
        var extremaTiming = (MaxInHours: (int?)null, MinInHours: (int?)null);

        var forecasts = current.Entries
            .Where(e => e.Key.Location == location)
            .Select(e => e.Value)
            .ToList();

        if (precipParam is not null && forecasts.Count > 0)
        {
            precipTiming = TrendAnalyzer.PrecipitationTiming(forecasts[0].Hourly, precipParam, now);
        }

        if (tempParam is not null && forecasts.Count > 0)
        {
            extremaTiming = TrendAnalyzer.ExtremaTiming(forecasts[0].Hourly, tempParam, now);
        }

        if (previous is not null && tempParam is not null)
        {
            var currIqr = ComputeIqrAtHorizon(current, tempParam, location, referenceHorizon, now);
            var prevIqr = ComputeIqrAtHorizon(previous, tempParam, location, referenceHorizon, now);
            stability = TrendAnalyzer.ConsensusStability(prevIqr, currIqr);
        }

        (double DecayRate, int? ReliableHours)? decay = null;
        if (tempParam is not null)
        {
            var spreads = new List<(int, double?)>();
            foreach (var h in horizons)
            {
                var spread = ComputeSpreadAtHorizon(current, tempParam, location, h, now);
                spreads.Add((h, spread));
            }
            decay = TrendAnalyzer.PredictabilityDecay(spreads);
        }

        return new TrendResult(location, paramTrends, weatherChange, precipTiming, extremaTiming, stability, decay);
    }

    private static double? MedianAtHorizon(
        ModelSnapshot snapshot, ParameterDef param, string location, int horizonHours, DateTimeOffset now)
    {
        var targetTime = TimeAnchor.AtHorizon(now, horizonHours);
        var values = new List<double?>();
        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location) continue;
            var point = forecast.Hourly.Points.FirstOrDefault(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
            values.Add(point?.Get(param));
        }
        return ConsensusComputer.ComputeMedian(values);
    }

    private static double? ComputeIqrAtHorizon(
        ModelSnapshot snapshot, ParameterDef param, string location, int horizonHours, DateTimeOffset now)
    {
        var targetTime = TimeAnchor.AtHorizon(now, horizonHours);
        var values = new List<double?>();
        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location) continue;
            var point = forecast.Hourly.Points.FirstOrDefault(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
            values.Add(point?.Get(param));
        }
        return ConsensusComputer.ComputeIqr(values);
    }

    private static double? ComputeSpreadAtHorizon(
        ModelSnapshot snapshot, ParameterDef param, string location, int horizonHours, DateTimeOffset now)
    {
        var targetTime = TimeAnchor.AtHorizon(now, horizonHours);
        var values = new List<double?>();
        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location) continue;
            var point = forecast.Hourly.Points.FirstOrDefault(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
            values.Add(point?.Get(param));
        }
        return ConsensusComputer.ComputeSpread(values);
    }
}
