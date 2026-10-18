using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Tests.Shared;

// Configuration root (in-memory baseline + override file), the section options with their validators,
// and the writer and view, wired like the host does it.
public sealed class ConfigHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    public ConfigHarness(IDictionary<string, string?> baseline, string overridePath, TimeProvider? time = null)
    {
        var clock = time ?? TestTimeouts.AtEpoch();
        OverridePath = overridePath;
        Root = new ConfigurationBuilder()
            .AddJsonFile(AppSettingsDefaults.ShippedPath, optional: false, reloadOnChange: false)
            .AddInMemoryCollection(baseline)
            .AddJsonFile(Path.GetFullPath(overridePath), optional: true, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Root);
        Add<NjordOptions, NjordOptionsValidator>(services, NjordOptions.SectionName, replaceCollections: true);
        Add<PollingOptions, PollingOptionsValidator>(services, PollingOptions.SectionName);
        Add<ConsensusOptions, ConsensusOptionsValidator>(services, ConsensusOptions.SectionName);
        Add<AlertOptions, AlertOptionsValidator>(services, AlertOptions.SectionName, replaceCollections: true);
        Add<DerivedOptions, DerivedOptionsValidator>(services, DerivedOptions.SectionName);
        Add<TrendOptions, TrendOptionsValidator>(services, TrendOptions.SectionName);
        Add<IndexOptions, IndexOptionsValidator>(services, IndexOptions.SectionName);
        services.AddSingleton<ConfigView>();
        services.AddSingleton<AnalysisConfigView>();
        services.AddSingleton<IConfigWriter>(sp => new ConfigWriter(
            sp, Root, new ConfigOverrideFile(overridePath), clock));
        _provider = services.BuildServiceProvider();
    }

    public string OverridePath { get; }

    public IConfigurationRoot Root { get; }

    public IServiceProvider Services => _provider;

    public ConfigView View => _provider.GetRequiredService<ConfigView>();

    public AnalysisConfigView Analysis => _provider.GetRequiredService<AnalysisConfigView>();

    public IConfigWriter Writer => _provider.GetRequiredService<IConfigWriter>();

    public IOptionsMonitor<T> Monitor<T>() where T : class => _provider.GetRequiredService<IOptionsMonitor<T>>();

    public void Dispose() => _provider.Dispose();

    private static void Add<T, TValidator>(IServiceCollection services, string section, bool replaceCollections = false)
        where T : class
        where TValidator : class, IValidateOptions<T>
    {
        var builder = services.AddOptions<T>().BindConfiguration(section);
        if (replaceCollections)
        {
            builder.ReplaceCollectionDefaults();
        }

        services.AddSingleton<IValidateOptions<T>, TValidator>();
    }
}
