using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Ice;

public sealed class IceAlert(IOptions<IceConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Ice",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.Precipitation,
            OptionalWeather: WeatherParams.Rain | WeatherParams.SoilTemperature,
            Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
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
        var spread = ctx.Current.Ensemble.Points.SpreadAt(coldest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new IceAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, config.Value.Threshold,
            PeakValue: null, HoursUntil: icy[0].HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedLow: rounded, RainHours: icy.Count, SoilFrozen: soilFrozen);
    }
}
