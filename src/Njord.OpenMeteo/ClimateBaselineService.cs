using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo;

// Slow domain: fetched at startup and refreshed daily, independent of the poll cycle. Registered after LocationResolutionService.
public sealed class ClimateBaselineService(
    IOptions<NjordOptions> njord,
    IOptions<EnrichmentOptions> enrichments,
    IClimateClient climateClient,
    IClimateBaselineStore store,
    TimeProvider timeProvider,
    ILogger<ClimateBaselineService> logger) : BackgroundService
{
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!enrichments.Value.IsEnabled(nameof(EnrichmentOptions.ClimateAnomaly)))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var refreshed = await RefreshAsync(stoppingToken);
            await Task.Delay(refreshed ? RefreshInterval : RetryInterval, timeProvider, stoppingToken);
        }
    }

    internal async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        var baselines = new Dictionary<string, MonthlyBaseline[]>(store.Baselines, StringComparer.OrdinalIgnoreCase);
        var complete = true;
        foreach (var location in njord.Value.Locations)
        {
            try
            {
                var response = await climateClient.FetchAsync(location.Latitude, location.Longitude, cancellationToken);
                if (response?.ToMonthlyBaselines() is { } monthly)
                {
                    baselines[location.Name] = monthly;
                }
                else
                {
                    complete = false;
                    logger.LogWarning("No climate baseline available for {Location}", location.Name);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                complete = false;
                logger.LogWarning(ex, "Climate baseline fetch failed for {Location}", location.Name);
            }
        }

        if (baselines.Count > 0)
        {
            store.Replace(baselines);
        }

        return complete;
    }
}
