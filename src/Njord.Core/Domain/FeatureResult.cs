namespace Njord.Core.Domain;

public abstract record FeatureResult(Location Location) : PipelineEvent;
