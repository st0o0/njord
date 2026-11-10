using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.Ozone;

public sealed class OzoneObservation(IOptions<OzoneObservationConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.AirQuality,
        Name: "OzoneObservation",
        new DomainRequirements(AirQuality: AirQualityParams.Ozone),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "ozone", Unit: "µg/m³"));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } d ? new AqInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.AirQuality, ctx.Now) is { Ozone: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
