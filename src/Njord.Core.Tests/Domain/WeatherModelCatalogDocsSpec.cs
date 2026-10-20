using System.Text.Json;
using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class WeatherModelCatalogDocsSpec
{
    private sealed record DocsModel(string Id, string DisplayName, string Provider, string Region, int ResolutionKm, int ForecastDays);

    [Fact]
    public void The_config_builder_mirror_matches_the_catalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "data", "weather-models.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var json = File.ReadAllText(Path.Combine(dir.FullName, "docs", "data", "weather-models.json"));
        var docs = JsonSerializer.Deserialize<DocsModel[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(docs);
        Assert.Equal(
            WeatherModelCatalog.All.Select(m => new DocsModel(m.Id, m.DisplayName, m.Provider, m.Region, m.ResolutionKm, m.ForecastDays)),
            docs);
    }
}
