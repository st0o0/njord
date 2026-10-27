using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.CoastalFlood;

public sealed class CoastalFloodAlert(IOptions<CoastalFloodConfig> config) : FeatureRule<MarineWeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Marine,
        Name: "CoastalFlood",
        DefaultEnabled: false,
        new DomainRequirements(
            Marine: MarineParams.SeaLevelHeight | MarineParams.WaveHeight | MarineParams.WaveDirection,
            Weather: WeatherParams.WindSpeed | WeatherParams.WindDirection,
            Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override MarineWeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Marine: { } m, Weather: { } w, Ensemble: { } e } ? new(m, w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<MarineWeatherEnsembleInput> ctx)
    {
        var cfg = config.Value;
        var marine = ctx.Current.Marine.NextHoursInclusive(ctx.Now);
        var weather = ctx.Current.Weather.NextHoursInclusive(ctx.Now);

        double? maxSeaLevel = null;
        double? maxWaveHeight = null;
        DateTimeOffset? peakTime = null;

        foreach (var p in marine)
        {
            if (p.SeaLevelHeight is { } sl && (maxSeaLevel is null || sl > maxSeaLevel))
            {
                maxSeaLevel = sl;
                peakTime = p.ValidFrom;
            }

            if (p.WaveHeight is { } wh && (maxWaveHeight is null || wh > maxWaveHeight))
            {
                maxWaveHeight = wh;
            }
        }

        if (maxSeaLevel is null || maxSeaLevel < cfg.SeaLevelThresholdM)
        {
            return null;
        }

        var hasHighWaves = maxWaveHeight >= cfg.WaveHeightThresholdM;
        var hasStrongWind = weather.Any(p => p.WindSpeed >= cfg.WindSpeedThreshold);

        var factors = (hasHighWaves ? 1 : 0) + (hasStrongWind ? 1 : 0);
        var severity = (maxSeaLevel.Value, factors) switch
        {
            ( >= 0.8, >= 1) => AlertSeverity.Red,
            ( >= 0.5, _) => AlertSeverity.Orange,
            (_, >= 2) => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        var spread = peakTime.HasValue ? ctx.Current.Ensemble.Points.SpreadAt(peakTime.Value) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new CoastalFloodAlertResult(
            ctx.Location, severity, level.ToConfidence(),
            Math.Round(maxSeaLevel.Value, 2), cfg.SeaLevelThresholdM,
            PeakValue: maxWaveHeight.HasValue ? Math.Round(maxWaveHeight.Value, 1) : null,
            HoursUntil: peakTime.HasValue ? (int?)Math.Max(0, (int)(peakTime.Value - ctx.Now).TotalHours) : null,
            DurationHours: null, Level: level,
            SeaLevelDeviation: Math.Round(maxSeaLevel.Value, 2),
            MaxWaveHeight: maxWaveHeight.HasValue ? Math.Round(maxWaveHeight.Value, 1) : null);
    }
}
