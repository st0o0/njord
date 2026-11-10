using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.ShortwaveRadiation;

public sealed class ShortwaveRadiationObservation(IOptions<ShortwaveRadiationObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "ShortwaveRadiationObservation",
        new DomainRequirements(Forecast: ForecastParams.ShortwaveRadiation),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "irradiance", Unit: "W/m²"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new ForecastInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { ShortwaveRadiation: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
