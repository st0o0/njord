using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class GeocodingClient(HttpClient httpClient) : IGeocodingClient
{
    public async Task<GeocodingResult?> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync(
                BuildUri(name), OpenMeteoJsonContext.Default.GeocodingResponse, cancellationToken);
            return response?.Results is { Length: > 0 } results ? results[0] : null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    internal static string BuildUri(string name) =>
        $"v1/search?name={Uri.EscapeDataString(name)}&count=1&language=en&format=json";
}
