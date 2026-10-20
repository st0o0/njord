using System.Net;
using System.Net.Http.Json;

namespace Njord.OpenMeteo;

public sealed class SeasonalClient(HttpClient httpClient) : ISeasonalClient
{
    public async Task<SeasonalResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, CancellationToken cancellationToken)
    {
        var uri = OpenMeteoRequestBuilder.To("v1/seasonal")
            .WithLocation(latitude, longitude)
            .WithMonthly(variables)
            .Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.SeasonalResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
