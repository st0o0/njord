using System.Net.Sockets;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Budget;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Gateway;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Marine;
using Njord.OpenMeteo.Satellite;
using Njord.OpenMeteo.Seasonal;
using Njord.OpenMeteo.Weather;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.OpenMeteo.Configuration;

public sealed class OpenMeteoSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<WeatherOptions, OpenMeteoOptionsValidator>();
        services.AddNjordOptions<AirQualityOptions, OpenMeteoOptionsValidator>();
        services.AddNjordOptions<MarineOptions, OpenMeteoOptionsValidator>();
        services.AddNjordOptions<FloodOptions, OpenMeteoOptionsValidator>();
        services.AddNjordOptions<SeasonalOptions, OpenMeteoOptionsValidator>();
        services.AddNjordOptions<SatelliteOptions, OpenMeteoOptionsValidator>();
        services.AddNjordOptions<SolarYieldOptions, OpenMeteoOptionsValidator>();

        services.AddSingleton<IBudgetProvider, OptionsBudgetProvider>();
        services.TryAddSingleton(FetchTimeouts.Default);

        AddDomainClient<WeatherClient>(services, OpenMeteoEndpoints.WeatherHost);
        AddDomainClient<AirQualityClient>(services, OpenMeteoEndpoints.AirQualityHost);
        AddDomainClient<FloodClient>(services, OpenMeteoEndpoints.FloodHost);
        AddDomainClient<MarineClient>(services, OpenMeteoEndpoints.MarineHost);
        AddDomainClient<SeasonalClient>(services, OpenMeteoEndpoints.SeasonalHost);
        AddDomainClient<SatelliteRadiationClient>(services, OpenMeteoEndpoints.SatelliteHost);

        services.AddSingleton<IReadOnlyDictionary<DomainKind, IDomainClient>>(provider =>
            provider.GetServices<IDomainClient>().ToDictionary(c => c.Domain));

        services.AddSingleton<IActorRegistration>(new GatewayActorRegistration());
    }

    private static void AddDomainClient<TClient>(IServiceCollection services, string baseAddress)
        where TClient : class, IDomainClient
    {
        var builder = services.AddHttpClient<TClient>(client => client.BaseAddress = new Uri(baseAddress))
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

        builder.AddHttpMessageHandler(() => new HttpMetricsHandler());
        builder.AddStandardResilienceHandler();
        services.AddTransient<IDomainClient>(provider => provider.GetRequiredService<TClient>());
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
