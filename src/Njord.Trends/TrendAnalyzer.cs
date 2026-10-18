using Microsoft.Extensions.Options;
using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Trends;
using Njord.Trends.Configuration;
using Njord.Model;

namespace Njord.Trends;

public sealed class TrendAnalyzer(IOptions<TrendOptions> options)
{
    private const int ExtremaWindowHours = 24;
    private const double DefaultThreshold = 0.5;
    private static readonly Horizon Reference = Horizon.Hours(3);

    private static readonly IReadOnlyDictionary<ParameterDef, double> TrendThresholds = new Dictionary<ParameterDef, double>
    {
        [ParameterRegistry.Temperature2m] = 0.5,
        [ParameterRegistry.WindSpeed10m] = 0.5,
        [ParameterRegistry.Precipitation] = 0.5,
        [ParameterRegistry.CloudCover] = 5.0,
    };

    private static readonly ParameterDef[] TrendParameters =
    [
        ParameterRegistry.Temperature2m,
        ParameterRegistry.WindSpeed10m,
        ParameterRegistry.Precipitation,
        ParameterRegistry.CloudCover,
    ];

    public bool Enabled { get; } = options.Value.Enabled;

    public TrendResult? Compute(LocationConsensus consensus, LocationConsensus? previous, TrendSettings settings) =>
        Enabled && previous is not null ? Analyze(consensus, previous) : null;

    public static TrendResult Analyze(LocationConsensus current, LocationConsensus? previous)
    {
        var parameterTrends = TrendParameters.ToDictionary(
            p => p.ApiName,
            p => previous is null ? null : ParameterTrend(current, previous, p));

        WeatherChangeResult? weatherChange = null;
        StabilityInfo? stability = null;
        if (previous is not null)
        {
            weatherChange = TrendFormulas.WeatherChange(
                WeatherCodeAt(previous), WeatherCodeAt(current));

            var temperature = ParameterRegistry.Temperature2m;
            var stabilityResult = TrendFormulas.ConsensusStability(
                previous.Hourly.Find(temperature).At(Reference)?.Iqr,
                current.Hourly.Find(temperature).At(Reference)?.Iqr);
            stability = stabilityResult is { } s ? new StabilityInfo(s.Stability, s.Ratio) : null;
        }

        return new TrendResult(
            current.Location, parameterTrends, weatherChange,
            PrecipitationTiming(current), ExtremaTiming(current), stability, Decay(current));
    }

    private static ParameterTrend? ParameterTrend(LocationConsensus current, LocationConsensus previous, ParameterDef parameter)
    {
        var trend = TrendFormulas.Direction(
            previous.Hourly.Find(parameter).MedianAt(Reference),
            current.Hourly.Find(parameter).MedianAt(Reference),
            TrendThresholds.GetValueOrDefault(parameter, DefaultThreshold));
        return trend is { } t ? new ParameterTrend(t.Direction, t.Delta) : null;
    }

    private static int? WeatherCodeAt(LocationConsensus consensus) =>
        consensus.Hourly.Find(ParameterRegistry.WeatherCode).MedianAt(Reference) is { } code ? (int)Math.Round(code) : null;

    // Precipitation timing and decay look at every hourly horizon of the series.
    private static HorizonWindow AllHours(ParameterConsensus series) => series.Within(Horizon.Hours(int.MaxValue));

    private static PrecipTimingInfo PrecipitationTiming(LocationConsensus consensus)
    {
        if (consensus.Hourly.Find(ParameterRegistry.Precipitation) is not { } precipitation)
        {
            return new PrecipTimingInfo(null, null);
        }

        int? first = null, last = null;
        foreach (var hit in AllHours(precipitation).Values().Where(hit => hit.Value > 0))
        {
            var hours = hit.Horizon.Offset;
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

    private static ExtremaTimingInfo ExtremaTiming(LocationConsensus consensus)
    {
        if (consensus.Hourly.Find(ParameterRegistry.Temperature2m) is not { } temperature)
        {
            return new ExtremaTimingInfo(null, null);
        }

        var window = temperature.Within(Horizon.Hours(ExtremaWindowHours));
        if (window.Values().Count() < 2)
        {
            return new ExtremaTimingInfo(null, null);
        }

        return new ExtremaTimingInfo(
            window.Extreme(ScanDirection.Max)?.Horizon.Offset,
            window.Extreme(ScanDirection.Min)?.Horizon.Offset);
    }

    private static DecayInfo? Decay(LocationConsensus consensus)
    {
        if (consensus.Hourly.Find(ParameterRegistry.Temperature2m) is not { } temperature)
        {
            return null;
        }

        var spreads = AllHours(temperature).Entries
            .Select(e => (e.Horizon.Offset, e.Consensus.Spread))
            .ToList();
        if (spreads.Count == 0)
        {
            return null;
        }

        spreads.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        var decay = TrendFormulas.PredictabilityDecay(spreads);
        return decay is { } d ? new DecayInfo(d.DecayRate, d.ReliableHours) : null;
    }
}
