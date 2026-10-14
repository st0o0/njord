using System.Net.Sockets;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Polling;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.OpenMeteo.Configuration;

public sealed class OpenMeteoSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<OpenMeteoOptions>()
            .BindConfiguration(OpenMeteoOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OpenMeteoOptions>, OpenMeteoOptionsValidator>();

        services.AddSingleton<IBudgetProvider, OptionsBudgetProvider>();
        services.TryAddSingleton(FetchTimeouts.Default);

        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((sp, client) =>
            {
                // Runs when the typed client is activated, not at registration.
                var options = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
                client.BaseAddress = new Uri(options.OpenMeteoBaseUrl);
                client.Timeout = sp.GetRequiredService<FetchTimeouts>().Http;
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
