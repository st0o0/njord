using System.Net;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Infrastructure;

public sealed class FakeOpenMeteoHandler : DelegatingHandler
{
    private readonly Dictionary<string, string> _responses = new();

    public void SetResponse(string modelId, string fixtureFileName)
    {
        _responses[modelId] = FixtureReader.Read(fixtureFileName);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri?.ToString() ?? "";

        foreach (var (modelId, json) in _responses)
        {
            if (uri.Contains($"models={modelId}", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":true,\"reason\":\"No fixture for this request\"}", System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
