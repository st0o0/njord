using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class OptionsRegistrationExtensionsTests
{
    [Fact]
    public void AddNjordOptions_applies_defaults_for_IHasArrayDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Test:Enabled", "true")])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddNjordOptions<FakeArrayConfig>();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FakeArrayConfig>>().Value;

        Assert.Equal(FakeArrayConfig.DefaultThresholds, options.Thresholds);
        Assert.True(options.Enabled);
    }

    [Fact]
    public void AddNjordOptions_preserves_user_provided_array()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("Test:Thresholds:0", "99"),
                new("Test:Thresholds:1", "88"),
            ])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddNjordOptions<FakeArrayConfig>();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FakeArrayConfig>>().Value;

        Assert.Equal([99, 88], options.Thresholds);
    }

    [Fact]
    public void AddNjordOptions_does_not_register_PostConfigure_for_non_array_config()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Simple:Enabled", "true")])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddNjordOptions<FakeSimpleConfig>();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FakeSimpleConfig>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal(42, options.Value);
    }

    private sealed class FakeArrayConfig : IConfigSection, IHasArrayDefaults
    {
        private const string SectionName = "Test";
        static string IConfigSection.SectionName => SectionName;
        public bool Enabled { get; set; }
        public double[] Thresholds { get; set; } = [];
        public static double[] DefaultThresholds => [1, 2, 3];

        public void ApplyDefaults()
        {
            if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
        }
    }

    private sealed class FakeSimpleConfig : IConfigSection
    {
        private const string SectionName = "Simple";
        static string IConfigSection.SectionName => SectionName;
        public bool Enabled { get; set; }
        public int Value { get; set; } = 42;
    }
}
