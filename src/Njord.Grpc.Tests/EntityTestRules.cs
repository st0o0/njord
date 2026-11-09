using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Grpc.Tests;

internal sealed class EntityTestRule(string name, OutputKind output = OutputKind.Alert, bool enabled = true) : IFeatureRule
{
    public FeatureDescriptor Descriptor { get; } = new(output, FeatureCategory.Forecast, name, new DomainRequirements());

    public bool Enabled => enabled;

    public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
}
