using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Grpc.Tests;

public static class TestServiceInfo
{
    public static IOptions<T> Opts<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);

    public abstract class StubRule(FeatureCategory category, string name, bool enabled = true) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Score, category, name, DefaultEnabled: false, default);

        public bool Enabled { get; } = enabled;

        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    public sealed class Bbq() : StubRule(FeatureCategory.Lifestyle, "Bbq");

    public sealed class FloodRisk() : StubRule(FeatureCategory.Flood, "FloodRisk");

    public sealed class FloodRain(bool enabled = true) : StubRule(FeatureCategory.Flood, "FloodRain", enabled);

    public sealed class FrostAlert() : StubRule(FeatureCategory.Forecast, "Frost");
}
