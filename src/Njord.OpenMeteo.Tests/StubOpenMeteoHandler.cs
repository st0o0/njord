using System.Net;
using System.Text;

namespace Njord.OpenMeteo.Tests;

internal sealed class StubOpenMeteoHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public static StubOpenMeteoHandler Json(HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect = null) =>
        new(request =>
        {
            inspect?.Invoke(request);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });

    public static HttpClient ClientFor(string host, HttpStatusCode status, string body, Action<HttpRequestMessage>? inspect = null) =>
        new(Json(status, body, inspect)) { BaseAddress = new Uri($"https://{host}.open-meteo.com/") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(respond(request));
    }
}
