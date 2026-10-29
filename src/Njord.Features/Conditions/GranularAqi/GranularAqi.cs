using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.GranularAqi;

public sealed class GranularAqi(IOptions<GranularAqiConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.AirQuality,
        Name: "GranularAqi",
        DefaultEnabled: false,
        new DomainRequirements(AirQuality: AirQualityParams.EuropeanAqiPm25 | AirQualityParams.EuropeanAqiPm10
            | AirQualityParams.EuropeanAqiNitrogenDioxide | AirQualityParams.EuropeanAqiOzone
            | AirQualityParams.EuropeanAqiSulphurDioxide));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } aq ? new(aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx)
    {
        var current = ctx.Current.AirQuality.NextHoursInclusive(ctx.Now).FirstOrDefault();
        if (current is null)
        {
            return null;
        }

        return new GranularAqiResult(
            ctx.Location,
            Pm25Aqi: current.EuropeanAqiPm25,
            Pm10Aqi: current.EuropeanAqiPm10,
            No2Aqi: current.EuropeanAqiNitrogenDioxide,
            OzoneAqi: current.EuropeanAqiOzone,
            So2Aqi: current.EuropeanAqiSulphurDioxide);
    }
}
