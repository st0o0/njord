using Njord.Configuration;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        IEnumerable<IFeatureRule>? rules = null) =>
        ServiceInfoMappings.ToServiceInfo(
            scope ?? AppSettingsDefaults.Options<NjordOptions>(),
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            rules ?? [new Commute(), new Pollen(), new Pm25()]);

    public abstract class StubRule(FeatureCategory category, bool enabled = true) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Score, category, DefaultEnabled: false, default);

        public bool Enabled { get; } = enabled;

        public IReadOnlyList<PipelineEvent> Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => [];
    }

    public sealed class Commute() : StubRule(FeatureCategory.Lifestyle);

    public sealed class Pollen() : StubRule(FeatureCategory.AirQuality);

    public sealed class Pm25() : StubRule(FeatureCategory.AirQuality);

    public sealed class Bbq() : StubRule(FeatureCategory.Lifestyle);

    public sealed class SolarYield() : StubRule(FeatureCategory.Energy);

    public sealed class FloodRisk() : StubRule(FeatureCategory.Flood);

    public sealed class FloodRain(bool enabled = true) : StubRule(FeatureCategory.Flood, enabled);
}
