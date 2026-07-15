using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Configuration;

namespace Njord.Ingest;

public static class IngestServiceCollectionExtensions
{
    public static IServiceCollection AddOpenMeteoIngest(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
            client.BaseAddress = new Uri(options.OpenMeteoBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
