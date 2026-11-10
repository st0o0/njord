using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.DischargeMean;

public sealed class DischargeMeanObservation(IOptions<DischargeMeanObservationConfig> config) : FeatureRule<FloodInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Flood,
        Name: "DischargeMeanObservation",
        new DomainRequirements(Flood: FloodParams.DischargeMean),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "volume_flow_rate", Unit: "m³/s"));

    public override bool Enabled => config.Value.Enabled;

    protected override FloodInput? Project(LocationForecast forecast) =>
        forecast.Flood is { } d ? new FloodInput(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<FloodInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Flood, ctx.Now) is { DischargeMean: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
