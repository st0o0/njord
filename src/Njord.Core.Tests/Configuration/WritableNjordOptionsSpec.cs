using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Domain.Options;

namespace Njord.Core.Tests.Configuration;

public sealed class WritableNjordOptionsSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-writable-{Guid.NewGuid():N}");

    [Fact]
    public void Update_persists_mutation_to_section_wrapped_json()
    {
        var (writable, _) = CreateWritable();

        writable.Update(opt => opt.Enrichment.Alerts.Enabled = false);

        var json = File.ReadAllText(OverridePath());
        Assert.Contains("\"Njord\"", json);
        Assert.Contains("\"Enabled\": false", json);
    }

    [Fact]
    public void Update_returns_post_mutation_snapshot()
    {
        var (writable, _) = CreateWritable();

        var result = writable.Update(opt => opt.Enrichment.Alerts.Enabled = false);

        Assert.False(result.Enrichment.Alerts.Enabled);
    }

    [Fact]
    public void Override_file_not_created_at_startup()
    {
        CreateWritable();

        Assert.False(File.Exists(OverridePath()));
    }

    [Fact]
    public void Override_file_created_on_first_mutation()
    {
        var (writable, _) = CreateWritable();

        writable.Update(opt => opt.ForecastDays = 7);

        Assert.True(File.Exists(OverridePath()));
    }

    [Fact]
    public void Mutation_overrides_baseline_config()
    {
        var (writable, monitor) = CreateWritable(opt => opt.Enrichment.Alerts.Enabled = true);

        Assert.True(monitor.CurrentValue.Enrichment.Alerts.Enabled);

        writable.Update(opt => opt.Enrichment.Alerts.Enabled = false);

        Assert.False(monitor.CurrentValue.Enrichment.Alerts.Enabled);
    }

    [Fact]
    public void Sequential_mutations_accumulate()
    {
        var (writable, _) = CreateWritable();

        writable.Update(opt => opt.ForecastDays = 7);
        writable.Update(opt => opt.Enrichment.Alerts.Enabled = false);

        var result = writable.Value;
        Assert.Equal(7, result.ForecastDays);
        Assert.False(result.Enrichment.Alerts.Enabled);
    }

    [Fact]
    public void Override_file_contains_full_section_wrapped_options()
    {
        var (writable, _) = CreateWritable();
        writable.Update(opt => opt.Enrichment.Alerts.Enabled = false);

        var (_, monitor2) = CreateWritable();
        Assert.False(monitor2.CurrentValue.Enrichment.Alerts.Enabled);
    }

    [Fact]
    public void Deep_clone_does_not_mutate_original()
    {
        var original = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };

        var clone = WritableNjordOptions.DeepClone(original);
        clone.Locations[0].Name = "zurich";
        clone.Models[0] = "gfs_seamless";

        Assert.Equal("lucerne", original.Locations[0].Name);
        Assert.Equal("icon_d2", original.Models[0]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string OverridePath() => Path.Combine(_tempDir, "appsettings.Override.json");

    private (WritableNjordOptions writable, IOptionsMonitor<NjordOptions> monitor) CreateWritable(
        Action<NjordOptions>? configureBaseline = null)
    {
        var baseline = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        configureBaseline?.Invoke(baseline);

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = baseline.Locations[0].Name,
            ["Njord:Locations:0:Latitude"] = baseline.Locations[0].Latitude.ToString(),
            ["Njord:Locations:0:Longitude"] = baseline.Locations[0].Longitude.ToString(),
            ["Njord:Models:0"] = baseline.Models[0],
            ["Njord:Enrichment:Alerts:Enabled"] = baseline.Enrichment.Alerts.Enabled.ToString(),
            ["Njord:ForecastDays"] = baseline.ForecastDays.ToString(),
        });
        configBuilder.AddJsonFile(OverridePath(), optional: true, reloadOnChange: false);

        var configRoot = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddOptions<NjordOptions>().Bind(configRoot.GetSection(NjordOptions.SectionName));
        var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<NjordOptions>>();

        var writable = new WritableNjordOptions(monitor, configRoot, OverridePath());
        return (writable, monitor);
    }
}
