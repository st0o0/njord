using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record HorizonDerived(
    [property: JsonProperty("beaufort")] int? Beaufort,
    [property: JsonProperty("windChill")] double? WindChill,
    [property: JsonProperty("dewPointComfort")] string? DewPointComfort,
    [property: JsonProperty("wmoDescription")] string? WmoDescription);

public sealed record ScalarDerived(
    [property: JsonProperty("diurnalAmplitude")] double? DiurnalAmplitude,
    [property: JsonProperty("sunshinePct")] double? SunshinePct,
    [property: JsonProperty("inversion")] bool? Inversion);

public sealed record DerivedResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("byHorizon")] IReadOnlyDictionary<string, HorizonDerived> ByHorizon,
    [property: JsonProperty("scalars")] ScalarDerived Scalars)
{
    public static DerivedResult Compute(
        ConsensusSnapshot consensus,
        IReadOnlyList<int> horizons,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider)
    {
        var windSpeed = parameters.Get(ParameterRegistry.WindSpeed10m);
        var temperature = parameters.Get(ParameterRegistry.Temperature2m);
        var dewPoint = parameters.Get(ParameterRegistry.DewPoint2m);
        var weatherCode = parameters.Get(ParameterRegistry.WeatherCode);
        var pressureMsl = parameters.Get(ParameterRegistry.PressureMsl);
        var surfacePressure = parameters.Get(ParameterRegistry.SurfacePressure);
        var sunshineDuration = parameters.Get(ParameterRegistry.SunshineDuration);
        var isDay = parameters.Get(ParameterRegistry.IsDay);

        var byHorizon = new Dictionary<string, HorizonDerived>();

        foreach (var hours in horizons)
        {
            var horizonKey = $"h{hours}";

            var windMedian = FindParam(consensus.Hourly.Parameters, windSpeed)
                ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
            var tempMedian = FindParam(consensus.Hourly.Parameters, temperature)
                ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
            var dpMedian = FindParam(consensus.Hourly.Parameters, dewPoint)
                ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
            var codeMedian = FindParam(consensus.Hourly.Parameters, weatherCode)
                ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;

            byHorizon[horizonKey] = new HorizonDerived(
                DerivedComputer.Beaufort(windMedian),
                DerivedComputer.WindChill(tempMedian, windMedian),
                DerivedComputer.DewPointComfort(dpMedian),
                DerivedComputer.WmoDescription(codeMedian.HasValue ? (int)Math.Round(codeMedian.Value) : null));
        }

        var scalarAmplitude = ComputeScalarAmplitude(consensus, temperature, timeProvider);
        var scalarSunshine = ComputeScalarSunshine(consensus, sunshineDuration, isDay, timeProvider);
        var scalarInversion = ComputeScalarInversion(consensus, pressureMsl, surfacePressure, temperature, dewPoint);

        var scalars = new ScalarDerived(scalarAmplitude, scalarSunshine, scalarInversion);
        return new DerivedResult(consensus.Location, byHorizon, scalars);
    }

    static ParameterConsensus? FindParam(IReadOnlyList<ParameterConsensus> parameters, ParameterDef? param)
        => param is null ? null : parameters.FirstOrDefault(p => p.Parameter == param);

    private static double? ComputeScalarAmplitude(
        ConsensusSnapshot consensus, ParameterDef? tempParam, TimeProvider timeProvider)
    {
        if (tempParam is null)
        {
            return null;
        }

        var tempConsensus = FindParam(consensus.Hourly.Parameters, tempParam);
        if (tempConsensus is null)
        {
            return null;
        }

        var maxHours = Math.Min(24, consensus.Hourly.CutoffHour);
        double? min = null, max = null;
        var count = 0;

        for (var h = 0; h <= maxHours; h++)
        {
            var horizonKey = $"h{h}";
            var median = tempConsensus.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
            if (median is not { } v)
            {
                continue;
            }

            count++;
            if (min is null || v < min)
            {
                min = v;
            }

            if (max is null || v > max)
            {
                max = v;
            }
        }

        return count < 2 ? null : max!.Value - min!.Value;
    }

    private static double? ComputeScalarSunshine(
        ConsensusSnapshot consensus, ParameterDef? sunshineDuration, ParameterDef? isDay, TimeProvider timeProvider)
    {
        if (sunshineDuration is null || isDay is null)
        {
            return null;
        }

        var sunshineConsensus = FindParam(consensus.Hourly.Parameters, sunshineDuration);
        var isDayConsensus = FindParam(consensus.Hourly.Parameters, isDay);
        if (sunshineConsensus is null || isDayConsensus is null)
        {
            return null;
        }

        var maxHours = Math.Min(24, consensus.Hourly.CutoffHour);
        double totalSunshineSec = 0;
        var daylightHours = 0;
        var hasSunshine = false;

        for (var h = 0; h <= maxHours; h++)
        {
            var horizonKey = $"h{h}";
            var isDayMedian = isDayConsensus.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
            if (isDayMedian is 1.0)
            {
                daylightHours++;
            }

            var sunshineMedian = sunshineConsensus.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
            if (sunshineMedian is { } s)
            {
                hasSunshine = true;
                totalSunshineSec += s;
            }
        }

        if (!hasSunshine || daylightHours == 0)
        {
            return null;
        }

        var daylightSec = daylightHours * 3600.0;
        return Math.Round(totalSunshineSec / daylightSec * 100.0, 1);
    }

    private static bool? ComputeScalarInversion(
        ConsensusSnapshot consensus, ParameterDef? pressureMsl, ParameterDef? surfacePressure,
        ParameterDef? temperature, ParameterDef? dewPoint)
    {
        if (pressureMsl is null || surfacePressure is null || temperature is null || dewPoint is null)
        {
            return null;
        }

        const string horizonKey = "h0";

        var mslMedian = FindParam(consensus.Hourly.Parameters, pressureMsl)
            ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
        var spMedian = FindParam(consensus.Hourly.Parameters, surfacePressure)
            ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
        var tempMedian = FindParam(consensus.Hourly.Parameters, temperature)
            ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;
        var dpMedian = FindParam(consensus.Hourly.Parameters, dewPoint)
            ?.ByHorizon.GetValueOrDefault(horizonKey)?.Median;

        return DerivedComputer.InversionDetected(mslMedian, spMedian, tempMedian, dpMedian);
    }
}
