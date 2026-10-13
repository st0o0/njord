using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed class TrendComputer
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

    public TrendResult Compute(
        ConsensusSnapshot current,
        ConsensusSnapshot? previous)
    {
        var paramTrends = new Dictionary<string, ParameterTrend?>();
        WeatherChangeResult? weatherChange = null;
        StabilityInfo? stability = null;

        var referenceKey = Horizon.Hours(3);

        foreach (var paramDef in TrendParamDefs)
        {
            if (previous is null)
            {
                paramTrends[paramDef.ApiName] = null;
                continue;
            }

            var currParam = FindParam(current.Hourly.Parameters, paramDef);
            var prevParam = FindParam(previous.Hourly.Parameters, paramDef);

            var currMedian = currParam?.ByHorizon.GetValueOrDefault(referenceKey)?.Median;
            var prevMedian = prevParam?.ByHorizon.GetValueOrDefault(referenceKey)?.Median;
            var threshold = Thresholds.GetValueOrDefault(paramDef, 0.5);

            var trend = TrendCalculator.Direction(prevMedian, currMedian, threshold);
            paramTrends[paramDef.ApiName] = trend is { } t ? new ParameterTrend(t.Direction, t.Delta) : null;
        }

        if (previous is not null)
        {
            var currWeatherCode = FindParam(current.Hourly.Parameters, ParameterRegistry.WeatherCode);
            var prevWeatherCode = FindParam(previous.Hourly.Parameters, ParameterRegistry.WeatherCode);

            var currCode = currWeatherCode?.ByHorizon.GetValueOrDefault(referenceKey)?.Median;
            var prevCode = prevWeatherCode?.ByHorizon.GetValueOrDefault(referenceKey)?.Median;

            weatherChange = TrendCalculator.WeatherChange(
                prevCode.HasValue ? (int)Math.Round(prevCode.Value) : null,
                currCode.HasValue ? (int)Math.Round(currCode.Value) : null);
        }

        var precipTiming = ComputePrecipTiming(current);
        var extremaTiming = ComputeExtremaTiming(current);

        if (previous is not null)
        {
            var currTempParam = FindParam(current.Hourly.Parameters, ParameterRegistry.Temperature2m);
            var prevTempParam = FindParam(previous.Hourly.Parameters, ParameterRegistry.Temperature2m);

            var currIqr = currTempParam?.ByHorizon.GetValueOrDefault(referenceKey)?.Iqr;
            var prevIqr = prevTempParam?.ByHorizon.GetValueOrDefault(referenceKey)?.Iqr;
            var stabilityResult = TrendCalculator.ConsensusStability(prevIqr, currIqr);
            stability = stabilityResult is { } s ? new StabilityInfo(s.Stability, s.Ratio) : null;
        }

        var decay = ComputeDecay(current);

        return new TrendResult(current.Location, paramTrends, weatherChange, precipTiming, extremaTiming, stability, decay);
    }

    private static PrecipTimingInfo ComputePrecipTiming(ConsensusSnapshot snapshot)
    {
        var precipParam = FindParam(snapshot.Hourly.Parameters, ParameterRegistry.Precipitation);
        if (precipParam is null)
        {
            return new PrecipTimingInfo(null, null);
        }

        int? first = null, last = null;

        foreach (var (key, hc) in precipParam.ByHorizon)
        {
            if (!key.IsHourly || hc.Median is not { } median || median <= 0)
            {
                continue;
            }

            var hours = key.Offset;
            if (first is null || hours < first)
            {
                first = hours;
            }

            if (last is null || hours > last)
            {
                last = hours;
            }
        }

        return new PrecipTimingInfo(first, last);
    }

    private static ExtremaTimingInfo ComputeExtremaTiming(ConsensusSnapshot snapshot)
    {
        var tempParam = FindParam(snapshot.Hourly.Parameters, ParameterRegistry.Temperature2m);
        if (tempParam is null)
        {
            return new ExtremaTimingInfo(null, null);
        }

        double? maxVal = null, minVal = null;
        int? maxHours = null, minHours = null;
        var count = 0;

        foreach (var (key, hc) in tempParam.ByHorizon)
        {
            if (!key.IsHourly || hc.Median is not { } median || key.Offset > 24)
            {
                continue;
            }

            var hours = key.Offset;

            count++;
            if (maxVal is null || median > maxVal)
            {
                maxVal = median;
                maxHours = hours;
            }
            if (minVal is null || median < minVal)
            {
                minVal = median;
                minHours = hours;
            }
        }

        return count < 2 ? new ExtremaTimingInfo(null, null) : new ExtremaTimingInfo(maxHours, minHours);
    }

    private static DecayInfo? ComputeDecay(ConsensusSnapshot snapshot)
    {
        var tempParam = FindParam(snapshot.Hourly.Parameters, ParameterRegistry.Temperature2m);
        if (tempParam is null)
        {
            return null;
        }

        var spreads = new List<(int, double?)>();
        foreach (var (key, hc) in tempParam.ByHorizon)
        {
            if (!key.IsHourly)
            {
                continue;
            }

            spreads.Add((key.Offset, hc.Spread));
        }

        if (spreads.Count == 0)
        {
            return null;
        }

        spreads.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        var decayResult = TrendCalculator.PredictabilityDecay(spreads);
        return decayResult is { } d ? new DecayInfo(d.DecayRate, d.ReliableHours) : null;
    }

    private static ParameterConsensus? FindParam(IReadOnlyList<ParameterConsensus> parameters, ParameterDef? param)
        => param is null ? null : parameters.FirstOrDefault(p => p.Parameter == param);
}
