using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Configuration;

namespace Njord.Ingest;

public static class IngestServiceCollectionExtensions
{
    public static IServiceCollection AddKachelmannIngest(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IKachelmannClient, KachelmannClient>(static (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<NjordOptions>>().Value;
            client.BaseAddress = new Uri("https://api.kachelmannwetter.com/v02/");
            client.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
