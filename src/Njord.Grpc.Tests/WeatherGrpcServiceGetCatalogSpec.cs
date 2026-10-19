using Akka.Actor;
using Grpc.Core;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceGetCatalogSpec : WeatherGrpcServiceSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetCatalog_returns_all_locations_with_resolved_models()
    {
        var service = CreateService();

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, response.Locations.Count);

        var lucerne = response.Locations[0];
        Assert.Equal("lucerne", lucerne.Name);
        Assert.Equal(47.05, lucerne.Latitude);
        Assert.Equal(8.31, lucerne.Longitude);
        Assert.Equal(["icon_d2", "ecmwf_ifs025"], lucerne.Models);

        var zurich = response.Locations[1];
        Assert.Equal("zurich", zurich.Name);
        Assert.Equal(["icon_d2", "ecmwf_ifs025"], zurich.Models);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task GetCatalog_deduplicates_model_info_across_locations()
    {
        var options = new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
                new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.55 },
            ],
            Models = ["icon_d2"],
        };
        var service = CreateService(options);

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(2, response.Locations.Count);
        var model = Assert.Single(response.Models);
        Assert.Equal("icon_d2", model.Id);
    }
}
