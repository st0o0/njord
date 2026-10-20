using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Alerts;

public sealed class FloodRainAlertRule : IAlertRule<FloodRainInput>
{
    public const int ForecastDays = 7;
    public const double HeavyRain24HourMillimeters = 30.0;


    public string Name => "FloodRain";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Flood,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Precipitation,
            Flood: FloodParams.Discharge | FloodParams.DischargeMedian | FloodParams.DischargeP75));

    public Alert Evaluate(FloodRainInput input, AlertOptions options, DateTimeOffset now)
    {
        var (flood, weather) = input;
        var rain = weather.NextHoursInclusive(now).Sum(p => p.Precipitation);
        if (rain < HeavyRain24HourMillimeters)
        {
            return Alert.None();
        }

        var points = flood.NextDays(now, ForecastDays).Where(p => p.Discharge is not null).ToList();
        if (points.MaxBy(p => p.Discharge) is not { Discharge: { } peakDischarge } peak)
        {
            return Alert.None();
        }

        var rising = peakDischarge > points[0].Discharge;
        if (!rising || peak.DischargeMedian is not { } median || peakDischarge <= median)
        {
            return Alert.None();
        }

        var aboveP75 = peak.DischargeP75 is { } p75 && peakDischarge > p75;
        return FloodRiskAlertRule.Build(
            aboveP75 ? AlertSeverity.Red : AlertSeverity.Orange,
            Math.Round(peakDischarge, 1),
            Math.Round(aboveP75 ? peak.DischargeP75!.Value : median, 1),
            peak.HoursFrom(now));
    }
}
