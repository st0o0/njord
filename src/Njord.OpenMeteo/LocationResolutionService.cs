using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

// Fills in coordinates of name-only locations before the actor system starts (registered ahead of Akka).
public sealed class LocationResolutionService(
    IOptions<NjordOptions> options,
    IGeocodingClient geocodingClient) : IHostedLifecycleService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // StartingAsync of every hosted service runs before any StartAsync, so the actor system starts with resolved coordinates.
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        foreach (var location in options.Value.Locations.Where(l => !l.HasCoordinates))
        {
            var result = await geocodingClient.ResolveAsync(location.Name, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Location '{location.Name}' has no Latitude/Longitude and geocoding found no match.");

            location.Latitude = result.Latitude;
            location.Longitude = result.Longitude;
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
