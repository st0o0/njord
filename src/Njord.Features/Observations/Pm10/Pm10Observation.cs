using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.Pm10;

public sealed class Pm10Observation(IOptions<Pm10ObservationConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.AirQuality,
        Name: "Pm10Observation",
        new DomainRequirements(AirQuality: AirQualityParams.Pm10),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "pm10", Unit: "µg/m³"));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.AirQuality, ctx.Now) is { Pm10: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
