using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        IEnumerable<IFeatureRule>? rules = null,
        EffectiveWeatherModels? models = null) =>
        (scope ?? AppSettingsDefaults.Options<NjordOptions>()).ToServiceInfo(
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            rules ?? [new Commute(), new Pollen(), new Pm25()],
            models ?? EffectiveWeatherModels.Default);

    public abstract class StubRule(FeatureCategory category, string name, bool enabled = true) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Score, category, name, DefaultEnabled: false, default);

        public bool Enabled { get; } = enabled;

        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    public sealed class Commute() : StubRule(FeatureCategory.Lifestyle, "Commute");

    public sealed class Pollen() : StubRule(FeatureCategory.AirQuality, "Pollen");

    public sealed class Pm25() : StubRule(FeatureCategory.AirQuality, "Pm25");

    public sealed class Bbq() : StubRule(FeatureCategory.Lifestyle, "Bbq");

    public sealed class SolarYield() : StubRule(FeatureCategory.Energy, "SolarYield");

    public sealed class FloodRisk() : StubRule(FeatureCategory.Flood, "FloodRisk");

    public sealed class FloodRain(bool enabled = true) : StubRule(FeatureCategory.Flood, "FloodRain", enabled);

    public sealed class FrostAlert() : StubRule(FeatureCategory.Weather, "Frost");
}
