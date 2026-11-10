using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Forecasts.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Forecasts;
using Njord.Messages.Stream;

namespace Njord.Features.Forecasts.EnsembleModelComparison;

public sealed class EnsembleModelComparison(
    IOptions<EnsembleModelComparisonConfig> config,
    IOptions<ActiveModels> activeModels) : FeatureRule<EnsembleModelsInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Forecast,
        FeatureCategory.Forecast,
        Name: "EnsembleModelComparison",
        new DomainRequirements(Ensemble: EnsembleParams.TemperatureSpread),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor));

    public override bool Enabled => config.Value.Enabled && activeModels.Value.EnsembleModels.Count >= 2;

    protected override EnsembleModelsInput? Project(LocationForecast forecast) =>
        forecast.EnsembleModelMap is { Count: >= 2 } models ? new(models) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<EnsembleModelsInput> ctx)
    {
        var days = ModelComparisonHelper.CompareDays(ctx.Current.Models, ctx.Now);
        return days.IsEmpty ? null : new EnsembleModelComparisonResult(ctx.Location, days);
    }
}
