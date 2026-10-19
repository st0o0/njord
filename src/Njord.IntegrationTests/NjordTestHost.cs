using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.OpenMeteo;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests;

// The production composition (NjordHost, the call Program.cs makes) on a test server: real setup containers, real
// actor system, real SQLite journal in a unique temporary directory, a fake Open-Meteo client. The in-memory
// server speaks HTTP/1.1 and HTTP/2, so the gRPC services are called over a real GrpcChannel; the Kestrel port
// split is not exercised.
public sealed class NjordTestHost : IAsyncDisposable
{
    private readonly GrpcChannel _channel;

    private NjordTestHost(WebApplication app, FakeOpenMeteoClient openMeteo, string dataDirectory)
    {
        App = app;
        OpenMeteo = openMeteo;
        DataDirectory = dataDirectory;
        Http = app.GetTestClient();
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = app.GetTestServer().CreateHandler() });
    }

    public WebApplication App { get; }

    public FakeOpenMeteoClient OpenMeteo { get; }

    public string DataDirectory { get; }

    public HttpClient Http { get; }

    public GrpcChannel Channel => _channel;

    public static IReadOnlyDictionary<string, string?> DefaultSettings(string dataDirectory) => new Dictionary<string, string?>
    {
        ["Serilog:MinimumLevel:Default"] = "Warning",
        ["Serilog:MinimumLevel:Override:Akka"] = "Error",
        ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Error",
        ["Njord:Storage:PersistencePath"] = Path.Combine(dataDirectory, "njord.db"),
        ["Njord:Models:0"] = "icon_eu",
        ["Njord:Models:1"] = "icon_global",
        ["Njord:Locations:0:Name"] = "luzern",
        ["Njord:Locations:0:Latitude"] = "47.0502",
        ["Njord:Locations:0:Longitude"] = "8.3093",
    };

    public static async Task<NjordTestHost> StartAsync(
        string dataDirectory,
        Action<IDictionary<string, string?>>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var settings = new Dictionary<string, string?>(DefaultSettings(dataDirectory));
        configure?.Invoke(settings);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "IntegrationTest",
        });
        builder.Configuration.AddJsonFile(AppSettingsDefaults.ShippedPath, optional: false, reloadOnChange: false);
        builder.Configuration.AddInMemoryCollection(settings);

        var openMeteo = new FakeOpenMeteoClient();
        WebApplication? app = null;
        var runner = NjordHost.Create(
            builder,
            b =>
            {
                b.Services.AddSingleton<IOpenMeteoClient>(openMeteo);
                b.WebHost.UseTestServer();
                app = b.Build();
                return app;
            });

        await runner.StartAsync(cancellationToken);
        return new NjordTestHost(app ?? throw new InvalidOperationException("The host was not built."), openMeteo, dataDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Dispose();
        Http.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
    }
}
