using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Ingest;
using Servus.Core.Application.Startup;

namespace Njord.Ingest.Configuration;

public sealed class IngestSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((IServiceProvider sp, HttpClient client) =>
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
    }
}
