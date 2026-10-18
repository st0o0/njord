using Microsoft.Extensions.Options;
using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Derived.Configuration;
using Njord.Messages.Derived;
using Njord.Model;

namespace Njord.Derived;

public sealed class DerivedAnalyzer(IOptions<DerivedOptions> options)
{
    private const int ScalarWindowHours = 24;
    private const double SecondsPerHour = 3600.0;
    private static readonly Horizon Now = Horizon.Hours(0);

    public bool Enabled { get; } = options.Value.Enabled;

    public DerivedResult? Compute(LocationConsensus consensus, DerivedSettings settings) =>
        Enabled ? Analyze(consensus, settings) : null;

    public static DerivedResult Analyze(LocationConsensus consensus, DerivedSettings settings)
    {
        var hourly = consensus.Hourly;
        var wind = hourly.Find(ParameterRegistry.WindSpeed10m);
        var temperature = hourly.Find(ParameterRegistry.Temperature2m);
        var dewPoint = hourly.Find(ParameterRegistry.DewPoint2m);
        var weatherCode = hourly.Find(ParameterRegistry.WeatherCode);

        var byHorizon = new Dictionary<Horizon, HorizonDerived>();
        foreach (var horizon in settings.Horizons.Select(Horizon.Hours))
        {
            var windMedian = wind.MedianAt(horizon);
            var code = weatherCode.MedianAt(horizon);
            byHorizon[horizon] = new HorizonDerived(
                DerivedFormulas.Beaufort(windMedian),
                DerivedFormulas.WindChill(temperature.MedianAt(horizon), windMedian),
                DerivedFormulas.Comfort(dewPoint.MedianAt(horizon)),
                DerivedFormulas.Condition(code.HasValue ? (int)Math.Round(code.Value) : null));
        }

        var scalars = new ScalarDerived(
            Amplitude(temperature, hourly.CutoffHour),
            SunshinePercent(hourly),
            DerivedFormulas.InversionDetected(
                hourly.Find(ParameterRegistry.PressureMsl).MedianAt(Now),
                hourly.Find(ParameterRegistry.SurfacePressure).MedianAt(Now),
                temperature.MedianAt(Now),
                dewPoint.MedianAt(Now)));

        return new DerivedResult(consensus.Location, byHorizon, scalars);
    }

    private static IEnumerable<Horizon> ScalarWindow(int cutoffHour) =>
        Enumerable.Range(0, Math.Max(0, Math.Min(ScalarWindowHours, cutoffHour) + 1)).Select(Horizon.Hours);

    private static double? Amplitude(ParameterConsensus? temperature, int cutoffHour)
    {
        if (temperature is null)
        {
            return null;
        }

        double? min = null, max = null;
        var count = 0;
        foreach (var value in ScalarWindow(cutoffHour).Select(temperature.MedianAt).OfType<double>())
        {
            count++;
            if (min is null || value < min)
            {
                min = value;
            }

            if (max is null || value > max)
            {
                max = value;
            }
        }

        return count < 2 ? null : max - min;
    }

    private static double? SunshinePercent(HourlyConsensus hourly)
    {
        var sunshine = hourly.Find(ParameterRegistry.SunshineDuration);
        var isDay = hourly.Find(ParameterRegistry.IsDay);
        if (sunshine is null || isDay is null)
        {
            return null;
        }

        double totalSunshineSec = 0;
        var daylightHours = 0;
        var hasSunshine = false;
        foreach (var horizon in ScalarWindow(hourly.CutoffHour))
        {
            if (isDay.MedianAt(horizon) is 1.0)
            {
                daylightHours++;
            }

            if (sunshine.MedianAt(horizon) is { } seconds)
            {
                hasSunshine = true;
                totalSunshineSec += seconds;
            }
        }

        if (!hasSunshine || daylightHours == 0)
        {
            return null;
        }

        return Math.Round(totalSunshineSec / (daylightHours * SecondsPerHour) * 100.0, 1);
    }
}
