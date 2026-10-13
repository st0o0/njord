using System.Reflection;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Sensors;

namespace Njord.Core.Tests.Configuration;

public sealed class ConfigReloadPolicySpec
{
    private static IEnumerable<Type> BoundOptionsTypes() =>
        typeof(NjordOptions).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Options", StringComparison.Ordinal))
            .Where(t => t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true });

    [Fact]
    public void Every_options_type_with_a_section_name_is_classified()
    {
        var unclassified = BoundOptionsTypes().Where(t => !ConfigReloadPolicy.IsClassified(t)).ToList();

        Assert.Empty(unclassified);
    }

    [Fact]
    public void Forecast_scope_properties_require_a_restart()
    {
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<NjordOptions>(nameof(NjordOptions.Locations)));
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<NjordOptions>(nameof(NjordOptions.Models)));
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<NjordOptions>(nameof(NjordOptions.Horizons)));
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<NjordOptions>(nameof(NjordOptions.ForecastDays)));
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<NjordOptions>(nameof(NjordOptions.Parameters)));
    }

    [Fact]
    public void Analysis_parameters_are_hot()
    {
        Assert.Equal(ReloadMode.Hot, ConfigReloadPolicy.For<AlertOptions>(nameof(AlertOptions.FrostThresholds)));
        Assert.Equal(ReloadMode.Hot, ConfigReloadPolicy.For<IndexOptions>(nameof(IndexOptions.Preferences)));
    }

    [Fact]
    public void Polling_is_hot()
    {
        Assert.Equal(ReloadMode.Hot, ConfigReloadPolicy.For<PollingOptions>(nameof(PollingOptions.PollInterval)));
        Assert.Equal(ReloadMode.Hot, ConfigReloadPolicy.For<PollingOptions>(nameof(PollingOptions.BudgetOverride)));
    }

    [Fact]
    public void Feature_toggles_require_a_restart()
    {
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<AlertOptions>(nameof(AlertOptions.Enabled)));
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<IndexOptions>(nameof(IndexOptions.Enabled)));
    }

    [Fact]
    public void Infrastructure_sections_require_a_restart()
    {
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<MqttOptions>());
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<SensorOptions>());
        Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For<PersistenceOptions>());
    }

    [Fact]
    public void An_unclassified_type_is_rejected()
    {
        Assert.False(ConfigReloadPolicy.IsClassified(typeof(string)));
        Assert.Throws<ArgumentException>(() => ConfigReloadPolicy.For(typeof(string)));
    }
}
