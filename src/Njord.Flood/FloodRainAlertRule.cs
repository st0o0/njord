using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Flood;

public sealed record FloodRainInput(DomainForecast<FloodPoint> Flood, DomainForecast<WeatherPoint> Weather);

public sealed class FloodRainAlertRule : IAlertRule<FloodRainInput>
{
    public const double HeavyRain24HourMillimeters = 30.0;

    public AlertType Type => AlertType.FloodRain;

    public Alert Evaluate(FloodRainInput input, AlertOptions options, DateTimeOffset now)
    {
        var (flood, weather) = input;
        var rain = weather.Next24Hours(now).Sum(p => p.Precipitation);
        if (rain < HeavyRain24HourMillimeters)
        {
            return Alert.None(Type);
        }

        var points = flood.NextDays(now);
        if (points.Count == 0)
        {
            return Alert.None(Type);
        }

        var peak = points.MaxBy(p => p.Discharge)!;
        var rising = peak.Discharge > points[0].Discharge;
        if (!rising || peak.DischargeMedian is not { } median || peak.Discharge <= median)
        {
            return Alert.None(Type);
        }

        var aboveP75 = peak.DischargeP75 is { } p75 && peak.Discharge > p75;
        return FloodRiskAlertRule.Build(
            Type,
            aboveP75 ? AlertSeverity.Red : AlertSeverity.Orange,
            Math.Round(peak.Discharge, 1),
            Math.Round(aboveP75 ? peak.DischargeP75!.Value : median, 1),
            peak.HoursFrom(now));
    }
}
