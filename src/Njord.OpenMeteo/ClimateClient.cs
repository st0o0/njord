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
        var uri = OpenMeteoRequestBuilder.To("v1/climate")
            .WithLocation(latitude, longitude)
            .With("start_date", $"{BaselineStartYear}-01-01")
            .With("end_date", $"{BaselineEndYear}-12-31")
            .With("models", Model)
            .WithDaily(DailyVariables.Split(','))
            .Build();
        try
        {
            return await httpClient.GetFromJsonAsync(uri, OpenMeteoJsonContext.Default.ClimateResponse, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
