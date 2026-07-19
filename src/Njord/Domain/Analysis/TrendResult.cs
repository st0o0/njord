using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record ParameterTrend(
    [property: JsonProperty("direction")] string Direction,
    [property: JsonProperty("delta")] double Delta);

public sealed record PrecipTimingInfo(
    [property: JsonProperty("startsInHours")] int? StartsInHours,
    [property: JsonProperty("endsInHours")] int? EndsInHours);

public sealed record ExtremaTimingInfo(
    [property: JsonProperty("maxInHours")] int? MaxInHours,
    [property: JsonProperty("minInHours")] int? MinInHours);

public sealed record StabilityInfo(
    [property: JsonProperty("label")] string Label,
    [property: JsonProperty("ratio")] double Ratio);

public sealed record DecayInfo(
    [property: JsonProperty("decayRate")] double DecayRate,
    [property: JsonProperty("reliableHours")] int? ReliableHours);

public sealed record TrendResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("parameterTrends")] IReadOnlyDictionary<string, ParameterTrend?> ParameterTrends,
    [property: JsonProperty("weatherChange")] WeatherChangeResult? WeatherChange,
    [property: JsonProperty("precipTiming")] PrecipTimingInfo PrecipTiming,
    [property: JsonProperty("extremaTiming")] ExtremaTimingInfo ExtremaTiming,
    [property: JsonProperty("stability")] StabilityInfo? Stability,
    [property: JsonProperty("decay")] DecayInfo? Decay)
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
        StabilityInfo? stability = null;

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

        var precipTiming = new PrecipTimingInfo(null, null);
        var extremaTiming = new ExtremaTimingInfo(null, null);

        var forecasts = current.Entries
            .Where(e => e.Key.Location == location)
            .Select(e => e.Value)
            .ToList();

        if (precipParam is not null && forecasts.Count > 0)
        {
            var timing = TrendAnalyzer.PrecipitationTiming(forecasts[0].Hourly, precipParam, now);
            precipTiming = new PrecipTimingInfo(timing.StartsInHours, timing.EndsInHours);
        }

        if (tempParam is not null && forecasts.Count > 0)
        {
            var timing = TrendAnalyzer.ExtremaTiming(forecasts[0].Hourly, tempParam, now);
            extremaTiming = new ExtremaTimingInfo(timing.MaxInHours, timing.MinInHours);
        }

        if (previous is not null && tempParam is not null)
        {
            var currIqr = ComputeIqrAtHorizon(current, tempParam, location, referenceHorizon, now);
            var prevIqr = ComputeIqrAtHorizon(previous, tempParam, location, referenceHorizon, now);
            var stabilityResult = TrendAnalyzer.ConsensusStability(prevIqr, currIqr);
            stability = stabilityResult is { } s ? new StabilityInfo(s.Label, s.Ratio) : null;
        }

        DecayInfo? decay = null;
        if (tempParam is not null)
        {
            var spreads = new List<(int, double?)>();
            foreach (var h in horizons)
            {
                var spread = ComputeSpreadAtHorizon(current, tempParam, location, h, now);
                spreads.Add((h, spread));
            }
            var decayResult = TrendAnalyzer.PredictabilityDecay(spreads);
            decay = decayResult is { } d ? new DecayInfo(d.DecayRate, d.ReliableHours) : null;
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
            if (key.Location != location)
            {
                continue;
            }

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
            if (key.Location != location)
            {
                continue;
            }

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
            if (key.Location != location)
            {
                continue;
            }

            var point = forecast.Hourly.Points.FirstOrDefault(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
            values.Add(point?.Get(param));
        }
        return ConsensusComputer.ComputeSpread(values);
    }
}
