using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Tests.Shared;

// The default option values as the host sees them: the shipped appsettings.json bound the way the host binds it
// (list defaults replaced by configured lists). Every call returns a fresh, mutable instance.
public static class AppSettingsDefaults
{
    public static string ShippedPath { get; } = Path.Combine(AppContext.BaseDirectory, "shipped", "appsettings.json");

    public static IConfigurationRoot Configuration() =>
        new ConfigurationBuilder().AddJsonFile(ShippedPath, optional: false, reloadOnChange: false).Build();

    public static IConfigurationBuilder Builder() =>
        new ConfigurationBuilder().AddJsonFile(ShippedPath, optional: false, reloadOnChange: false);

    public static T Options<T>() where T : class => Options<T>(Configuration());

    public static T Options<T>(Action<T> configure) where T : class
    {
        var options = Options<T>();
        configure(options);
        return options;
    }

    public static T Options<T>(IConfiguration configuration) where T : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddOptions<T>().BindConfiguration(ConfigSections.PathOf<T>()).ReplaceCollectionDefaults();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<T>>().Value;
    }
}
