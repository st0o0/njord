using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class GeocodingClient(HttpClient httpClient) : IGeocodingClient
{
    public async Task<GeocodingResult?> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var uri = OpenMeteoRequestBuilder.To("v1/search")
                .WithName(name)
                .With("count", "1")
                .With("language", "en")
                .With("format", "json")
                .Build();
            var response = await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.GeocodingResponse, cancellationToken);
            return response?.Results is { Length: > 0 } results ? results[0] : null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
