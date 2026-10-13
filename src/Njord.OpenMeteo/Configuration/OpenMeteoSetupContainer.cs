using System.Net.Sockets;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.OpenMeteo.Configuration;

public sealed class OpenMeteoSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
                client.BaseAddress = new Uri(options.OpenMeteoBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
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

        services.AddSingleton<IActorRegistration>(new GatewayActorRegistration());
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
