using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Njord.Ingest;

public static class IngestServiceCollectionExtensions
{
    public static IServiceCollection AddOpenMeteoIngest(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>(static client =>
        {
            client.BaseAddress = new Uri("https://api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
