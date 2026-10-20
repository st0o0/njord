using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class ClimateClient(HttpClient httpClient) : IClimateClient
{
    internal const string DailyVariables = "temperature_2m_mean,precipitation_sum,sunshine_duration";
    internal const string Model = "EC_Earth3P_HR";
    internal const int BaselineStartYear = 1991;
    internal const int BaselineEndYear = 2020;

    public async Task<ClimateResponse?> FetchAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var uri = BuildUri(latitude, longitude);
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.ClimateResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    internal static string BuildUri(double latitude, double longitude) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"v1/climate?latitude={latitude}&longitude={longitude}" +
            $"&start_date={BaselineStartYear}-01-01&end_date={BaselineEndYear}-12-31" +
            $"&models={Model}&daily={DailyVariables}&timeformat=unixtime&timezone=UTC");
}
