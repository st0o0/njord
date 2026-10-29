using Njord.Core;
using Njord.Messages.Stream;

namespace Njord.Messages.Results;

public abstract record FeatureResult(Location Location) : PipelineEvent;
