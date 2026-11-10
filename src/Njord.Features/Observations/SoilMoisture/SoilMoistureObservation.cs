using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Observations.SoilMoisture;

public sealed class SoilMoistureObservation(IOptions<SoilMoistureObservationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Observation,
        FeatureCategory.Forecast,
        Name: "SoilMoistureObservation",
        new DomainRequirements(Forecast: ForecastParams.SoilMoisture0To1Cm),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, DeviceClass: "moisture", Unit: "%"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } d ? new(d) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx) =>
        ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { SoilMoisture0To1Cm: { } value }
            ? new ObservationResult(ctx.Location, Descriptor.Name, Math.Round(value * 100, 1))
            : null;
}
