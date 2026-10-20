using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.CommuteSafety;

public sealed class CommuteSafety(IOptions<CommuteSafetyConfig> config) : FeatureRule<WeatherInput>
{
    public const double IceTemperature = 1.0;
    public const double DewPointSpread = 2.0;
    public const double FogHumidity = 95.0;
    public const double HeavyRain = 5.0;
    public const double StrongWind = 60.0 / 3.6;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.DewPoint | WeatherParams.Humidity
            | WeatherParams.Precipitation | WeatherParams.WindSpeed));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var hours = ctx.Current.Weather.NextHours(ctx.Now, 2);
        if (hours.Count == 0)
        {
            return null;
        }

        var ice = hours.Any(p => p.Temperature < IceTemperature && p.Temperature - p.DewPoint < DewPointSpread);
        var fog = hours.Any(p => p.Humidity > FogHumidity && p.Temperature - p.DewPoint < DewPointSpread);
        var rain = hours.Any(p => p.Precipitation > HeavyRain);
        var wind = hours.Any(p => p.WindSpeed > StrongWind);

        var risks = new List<string>();
        if (ice) risks.Add("Ice");
        if (fog) risks.Add("Fog");
        if (rain) risks.Add("Heavy rain");
        if (wind) risks.Add("Strong wind");

        var level = risks.Count switch
        {
            0 => "Safe",
            1 when !ice => "Caution",
            _ => "Dangerous",
        };
        return new CommuteSafetyResult(ctx.Location, level, risks);
    }
}
