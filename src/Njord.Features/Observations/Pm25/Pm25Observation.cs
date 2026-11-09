using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.Pm25;

public sealed class Pm25Observation(IOptions<Pm25ObservationConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.AirQuality,
        Name: "Pm25Observation",
        new DomainRequirements(AirQuality: AirQualityParams.Pm25),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "pm25", Unit: "µg/m³"));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.AirQuality, ctx.Now) is { Pm25: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
