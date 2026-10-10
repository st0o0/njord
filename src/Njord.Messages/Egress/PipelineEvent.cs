using Njord.Weather;

namespace Njord.Messages.Egress;

public abstract record PipelineEvent
{
    public sealed record ModelForecastUpdate(
        string Location,
        WeatherModel Model,
        ModelForecast Forecast) : PipelineEvent;

    public sealed record AnalysisResult(string Location, string TypeName, object Result, DateTimeOffset? UpdatedAt = null) : PipelineEvent;

    public sealed record ModelCapability(
        string Location,
        WeatherModel Model,
        IReadOnlySet<ParameterDef> SupportedParameters,
        IReadOnlyList<int> ApplicableHorizons,
        IReadOnlyList<int> ApplicableDayOffsets) : PipelineEvent;
}
