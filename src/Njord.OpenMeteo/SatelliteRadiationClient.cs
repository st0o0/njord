using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo;

public sealed class SatelliteRadiationClient(HttpClient httpClient, IOptions<SolarYieldOptions> solar) : ISatelliteRadiationClient
{
    public async Task<SatelliteRadiationResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int pastHours, CancellationToken cancellationToken)
    {
        // Open-Meteo azimuth: 0 = south; SolarYieldOptions uses compass degrees (180 = south).
        var uri = OpenMeteoRequestBuilder.To("v1/archive")
            .WithLocation(latitude, longitude)
            .WithHourly(variables)
            .With("tilt", solar.Value.Tilt.ToString(CultureInfo.InvariantCulture))
            .With("azimuth", (solar.Value.Azimuth - 180).ToString(CultureInfo.InvariantCulture))
            .With("past_hours", pastHours.ToString(CultureInfo.InvariantCulture))
            .With("forecast_hours", "1")
            .Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.SatelliteRadiationResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
