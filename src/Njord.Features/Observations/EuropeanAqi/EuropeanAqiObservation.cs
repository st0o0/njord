using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.EuropeanAqi;

public sealed class EuropeanAqiObservation(IOptions<EuropeanAqiObservationConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.AirQuality,
        Name: "EuropeanAqiObservation",
        new DomainRequirements(AirQuality: AirQualityParams.EuropeanAqi),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "aqi", Unit: ""));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } d ? new AqInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.AirQuality, ctx.Now) is { EuropeanAqi: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
