using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.ElevatedWind;

public sealed class ElevatedWind(IOptions<ElevatedWindConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "ElevatedWind",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.WindSpeed80m | WeatherParams.WindDirection80m | WeatherParams.WindSpeed120m | WeatherParams.WindDirection120m));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        var current = window.FirstOrDefault();
        if (current is not { WindSpeed80m: { } speed80, WindDirection80m: { } dir80 })
        {
            return null;
        }

        return new ElevatedWindResult(
            ctx.Location,
            Speed80m: Math.Round(speed80, 1),
            Direction80m: Math.Round(dir80, 0),
            Speed120m: current.WindSpeed120m is { } s120 ? Math.Round(s120, 1) : null,
            Direction120m: current.WindDirection120m is { } d120 ? Math.Round(d120, 0) : null);
    }
}
