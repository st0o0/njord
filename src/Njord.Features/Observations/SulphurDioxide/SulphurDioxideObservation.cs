using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.SulphurDioxide;

public sealed class SulphurDioxideObservation(IOptions<SulphurDioxideObservationConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.AirQuality,
        Name: "SulphurDioxideObservation",
        new DomainRequirements(AirQuality: AirQualityParams.SulphurDioxide),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "sulphur_dioxide", Unit: "µg/m³"));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.AirQuality, ctx.Now) is { SulphurDioxide: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
