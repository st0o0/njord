using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.CommuteSafety;

public sealed class CommuteSafety(IOptions<CommuteSafetyConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Commute",
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.DewPoint | ForecastParams.Humidity
            | ForecastParams.Precipitation | ForecastParams.WindSpeed),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now, 2);
        if (hours.Count == 0)
        {
            return null;
        }

        var cfg = config.Value;
        var strongWindMs = cfg.StrongWindKmh / 3.6;
        var ice = hours.Any(p => p.Temperature < cfg.IceTemperatureCelsius && p.Temperature - p.DewPoint < cfg.DewPointSpreadCelsius);
        var fog = hours.Any(p => p.Humidity > cfg.FogHumidityPercent && p.Temperature - p.DewPoint < cfg.DewPointSpreadCelsius);
        var rain = hours.Any(p => p.Precipitation > cfg.HeavyRainMmH);
        var wind = hours.Any(p => p.WindSpeed > strongWindMs);

        var risks = new List<CommuteHazard>();
        if (ice)
        {
            risks.Add(CommuteHazard.Ice);
        }

        if (fog)
        {
            risks.Add(CommuteHazard.Fog);
        }

        if (rain)
        {
            risks.Add(CommuteHazard.HeavyRain);
        }

        if (wind)
        {
            risks.Add(CommuteHazard.StrongWind);
        }

        var level = risks.Count switch
        {
            0 => CommuteLevel.Safe,
            1 when !ice => CommuteLevel.Caution,
            _ => CommuteLevel.Dangerous,
        };
        return new CommuteSafetyResult(ctx.Location, level, [.. risks]);
    }
}
