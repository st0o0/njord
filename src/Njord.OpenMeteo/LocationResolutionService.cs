using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

// Fills in coordinates of name-only locations before the actor system starts (registered ahead of Akka).
public sealed class LocationResolutionService(
    IOptions<NjordOptions> options,
    IGeocodingClient geocodingClient) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
