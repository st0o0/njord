using System.Net.Sockets;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Polling;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.OpenMeteo.Configuration;

public sealed class OpenMeteoSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<OpenMeteoOptions>(configuration);

        services.AddSingleton<IBudgetProvider, OptionsBudgetProvider>();
        services.TryAddSingleton(FetchTimeouts.Default);

        AddTypedClient<IWeatherClient, WeatherClient>(services, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = sp.GetRequiredService<FetchTimeouts>().Http;
        });

        AddTypedClient<IAirQualityClient, AirQualityClient>(services, (_, client) =>
        {
            client.BaseAddress = new Uri("https://air-quality-api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        AddTypedClient<IFloodClient, FloodClient>(services, (_, client) =>
        {
            client.BaseAddress = new Uri("https://flood-api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        AddTypedClient<IMarineClient, MarineClient>(services, (_, client) =>
        {
            client.BaseAddress = new Uri("https://marine-api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        AddTypedClient<IGeocodingClient, GeocodingClient>(services, (_, client) =>
        {
            client.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHostedService<LocationResolutionService>();

        services.AddSingleton<IActorRegistration>(new GatewayActorRegistration());
    }

    private static void AddTypedClient<TInterface, TImplementation>(
        IServiceCollection services,
        Action<IServiceProvider, HttpClient> configure)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>(configure)
            .AddHttpMessageHandler(() => new HttpMetricsHandler())
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectCallback = async (context, ct) =>
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.NoDelay = true;
                    try
                    {
                        await socket.ConnectAsync(context.DnsEndPoint, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });
    }

    private sealed class GatewayActorRegistration : IActorRegistration
    {
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private const double RandomFactor = 0.2;

        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IForecastGateway>("gateway-supervisor",
                (_, _, resolver) => resolver.Props<OpenMeteoGateway>().BackoffOnFailure("gateway", MinBackoff,
                    MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1)));
        }
    }
}
