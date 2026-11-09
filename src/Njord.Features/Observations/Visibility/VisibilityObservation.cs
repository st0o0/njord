using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.Visibility;

public sealed class VisibilityObservation(IOptions<VisibilityObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "VisibilityObservation",
        new DomainRequirements(Forecast: ForecastParams.Visibility),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "distance", Unit: "m"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { Visibility: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, value)
            : null;
}
