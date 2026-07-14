using System.Text.Json.Nodes;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record HorizonDerived(
    int? Beaufort,
    double? WindChill,
    string? DewPointComfort,
    string? WmoDescription);

public sealed record ScalarDerived(
    double? DiurnalAmplitude,
    double? SunshinePct,
    bool? Inversion);

public sealed record DerivedResult(
    string Location,
    IReadOnlyDictionary<string, HorizonDerived> ByHorizon,
    ScalarDerived Scalars)
{
    public static DerivedResult Compute(
        ModelSnapshot snapshot,
        string location,
        IReadOnlyList<int> horizons,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();

        var windSpeed = parameters.Get(ParameterRegistry.WindSpeed10m);
        var temperature = parameters.Get(ParameterRegistry.Temperature2m);
        var dewPoint = parameters.Get(ParameterRegistry.DewPoint2m);
        var weatherCode = parameters.Get(ParameterRegistry.WeatherCode);
        var pressureMsl = parameters.Get(ParameterRegistry.PressureMsl);
        var surfacePressure = parameters.Get(ParameterRegistry.SurfacePressure);
        var sunshineDuration = parameters.Get(ParameterRegistry.SunshineDuration);
        var isDay = parameters.Get(ParameterRegistry.IsDay);

        var forecasts = snapshot.Entries
            .Where(e => e.Key.Location == location)
            .Select(e => e.Value)
            .ToList();

        var byHorizon = new Dictionary<string, HorizonDerived>();

        foreach (var hours in horizons)
        {
            var targetTime = TimeAnchor.AtHorizon(now, hours);
            var horizonKey = $"h{hours}";

            var windMedian = MedianAt(forecasts, windSpeed, targetTime);
            var tempMedian = MedianAt(forecasts, temperature, targetTime);
            var dpMedian = MedianAt(forecasts, dewPoint, targetTime);
            var codeMedian = MedianAt(forecasts, weatherCode, targetTime);

            byHorizon[horizonKey] = new HorizonDerived(
                DerivedComputer.Beaufort(windMedian),
                DerivedComputer.WindChill(tempMedian, windMedian),
                DerivedComputer.DewPointComfort(dpMedian),
                DerivedComputer.WmoDescription(codeMedian.HasValue ? (int)Math.Round(codeMedian.Value) : null));
        }

        var scalarAmplitude = ComputeScalarAmplitude(forecasts, temperature, now);
        var scalarSunshine = ComputeScalarSunshine(forecasts, sunshineDuration, isDay, now);
        var scalarInversion = ComputeScalarInversion(forecasts, pressureMsl, surfacePressure, temperature, dewPoint, now);

        var scalars = new ScalarDerived(scalarAmplitude, scalarSunshine, scalarInversion);
        return new DerivedResult(location, byHorizon, scalars);
    }

    private static double? MedianAt(
        List<ModelForecast> forecasts, ParameterDef? param, DateTimeOffset targetTime)
    {
        if (param is null) return null;

        var values = new List<double?>();
        foreach (var forecast in forecasts)
        {
            var point = forecast.Hourly.Points.FirstOrDefault(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
            values.Add(point?.Get(param));
        }
        return ConsensusComputer.ComputeMedian(values);
    }

    private static double? ComputeScalarAmplitude(
        List<ModelForecast> forecasts, ParameterDef? tempParam, DateTimeOffset now)
    {
        if (tempParam is null) return null;

        var amplitudes = new List<double?>();
        foreach (var forecast in forecasts)
        {
            amplitudes.Add(DerivedComputer.DiurnalAmplitude(forecast.Hourly, tempParam, now));
        }
        return ConsensusComputer.ComputeMedian(amplitudes);
    }

    private static double? ComputeScalarSunshine(
        List<ModelForecast> forecasts, ParameterDef? sunshineDuration, ParameterDef? isDay, DateTimeOffset now)
    {
        if (sunshineDuration is null || isDay is null) return null;

        var values = new List<double?>();
        foreach (var forecast in forecasts)
        {
            values.Add(DerivedComputer.SunshinePercent(forecast.Hourly, sunshineDuration, isDay, now));
        }
        return ConsensusComputer.ComputeMedian(values);
    }

    private static bool? ComputeScalarInversion(
        List<ModelForecast> forecasts, ParameterDef? pressureMsl, ParameterDef? surfacePressure,
        ParameterDef? temperature, ParameterDef? dewPoint, DateTimeOffset now)
    {
        if (pressureMsl is null || surfacePressure is null || temperature is null || dewPoint is null)
            return null;

        var targetTime = now;
        var trueCount = 0;
        var totalCount = 0;
        foreach (var forecast in forecasts)
        {
            var point = forecast.Hourly.Points.FirstOrDefault(p =>
                Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
            if (point is null) continue;

            var result = DerivedComputer.InversionDetected(
                point.Get(pressureMsl), point.Get(surfacePressure),
                point.Get(temperature), point.Get(dewPoint));
            if (result is not { } r) continue;

            totalCount++;
            if (r) trueCount++;
        }

        if (totalCount == 0) return null;
        return trueCount > totalCount / 2;
    }
}
