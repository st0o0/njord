using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Ice;

public sealed class IceAlert(IOptions<IceConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Ice",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.Precipitation | WeatherParams.EnsembleStdDev,
            OptionalWeather: WeatherParams.Rain | WeatherParams.SoilTemperature));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var icy = ctx.Current.Weather.NextHoursInclusive(ctx.Now)
            .Where(p => (p.Rain ?? p.Precipitation) > 0 && p.Temperature <= config.Value.Threshold)
            .ToList();
        if (icy.MinBy(p => p.Temperature) is not { Temperature: { } minTemp } coldest)
        {
            return null;
        }

        var soilFrozen = icy.Any(p => p.SoilTemperature is <= 0);
        var severity = (minTemp, soilFrozen) switch
        {
            ( <= 0, true) => AlertSeverity.Red,
            ( <= 0, false) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        var rounded = Math.Round(minTemp, 1);
        var level = ConfidenceThresholds.Default.LevelFor(coldest.EnsembleStdDev);
        return new IceAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, config.Value.Threshold,
            PeakValue: null, HoursUntil: icy[0].HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedLow: rounded, RainHours: icy.Count, SoilFrozen: soilFrozen);
    }
}
