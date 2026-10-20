using Njord.Forecast;

namespace Njord.Messages.Pipeline;

public abstract record FeatureResult(Location Location) : PipelineEvent;
