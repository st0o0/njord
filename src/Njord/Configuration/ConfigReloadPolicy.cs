using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Sensors;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Configuration;

public enum ReloadMode
{
    Hot,
    RestartRequired,
}

public static class ConfigReloadPolicy
{
    private const string EnabledProperty = "Enabled";

    private static readonly Dictionary<Type, ReloadMode> TypeModes = new()
    {
        [typeof(NjordOptions)] = ReloadMode.RestartRequired,
        [typeof(PollingOptions)] = ReloadMode.Hot,
        [typeof(ConsensusOptions)] = ReloadMode.Hot,
        [typeof(AlertOptions)] = ReloadMode.Hot,
        [typeof(IndexOptions)] = ReloadMode.Hot,
        [typeof(TrendOptions)] = ReloadMode.Hot,
        [typeof(DerivedOptions)] = ReloadMode.Hot,
        [typeof(MqttOptions)] = ReloadMode.RestartRequired,
        [typeof(SensorOptions)] = ReloadMode.RestartRequired,
        [typeof(PersistenceOptions)] = ReloadMode.RestartRequired,
        [typeof(StorageOptions)] = ReloadMode.RestartRequired,
        [typeof(OpenMeteoOptions)] = ReloadMode.RestartRequired,
    };

    private static readonly Dictionary<(Type Type, string Property), ReloadMode> PropertyModes = new()
    {
        [(typeof(NjordOptions), nameof(NjordOptions.Locations))] = ReloadMode.RestartRequired,
        [(typeof(NjordOptions), nameof(NjordOptions.Models))] = ReloadMode.RestartRequired,
        [(typeof(NjordOptions), nameof(NjordOptions.Horizons))] = ReloadMode.RestartRequired,
        [(typeof(NjordOptions), nameof(NjordOptions.ForecastDays))] = ReloadMode.RestartRequired,
        [(typeof(NjordOptions), nameof(NjordOptions.Parameters))] = ReloadMode.RestartRequired,
        [(typeof(ConsensusOptions), EnabledProperty)] = ReloadMode.RestartRequired,
        [(typeof(AlertOptions), EnabledProperty)] = ReloadMode.RestartRequired,
        [(typeof(IndexOptions), EnabledProperty)] = ReloadMode.RestartRequired,
        [(typeof(TrendOptions), EnabledProperty)] = ReloadMode.RestartRequired,
        [(typeof(DerivedOptions), EnabledProperty)] = ReloadMode.RestartRequired,
    };

    public static bool IsClassified(Type optionsType) => TypeModes.ContainsKey(optionsType);

    public static ReloadMode For<T>(string? property = null) => For(typeof(T), property);

    public static ReloadMode For(Type optionsType, string? property = null)
    {
        if (!TypeModes.TryGetValue(optionsType, out var typeMode))
        {
            throw new ArgumentException($"Options type '{optionsType.Name}' has no reload classification.", nameof(optionsType));
        }

        return property is not null && PropertyModes.TryGetValue((optionsType, property), out var propertyMode)
            ? propertyMode
            : typeMode;
    }
}
