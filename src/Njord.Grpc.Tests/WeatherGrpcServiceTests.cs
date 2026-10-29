using System.Collections.Immutable;
using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Seasonal;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Messages.Stream;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class WeatherGrpcServiceTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly NjordOptions Options = new()
    {
        Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }],
    };

    [Fact(Timeout = 30_000)]
    public async Task GetCatalog_returns_the_configured_locations_with_the_ensemble_model()
    {
        var service = CreateService();

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var location = Assert.Single(response.Locations);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", location.Name),
            () => Assert.Equal(47.05, location.Latitude),
            () => Assert.Equal([ForecastOptions.DefaultModel], location.Models));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_returns_the_latest_event_of_the_requested_domain()
    {
        var evt = new ForecastDataReceived(DomainData.Lucerne, DomainData.Forecast(Anchor, 3));
        var service = CreateService(domainEvent: evt);

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Forecast }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("Lucerne", response.Location),
            () => Assert.Equal(Domain.Forecast, response.Domain),
            () => Assert.Equal(Anchor, response.ModelRun.ToDateTimeOffset()),
            () => Assert.Equal(3, response.Forecast.Points.Count));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_returns_the_seasonal_months()
    {
        var forecast = new DomainForecast<SeasonalPoint>(
            DomainKind.Seasonal, Anchor, TimeResolution.Monthly, [new SeasonalPoint(Anchor, 1.5, -12.0, 80.0)]);
        var service = CreateService(domainEvent: new SeasonalDataReceived(DomainData.Lucerne, forecast));

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Seasonal },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var sample = Assert.Single(response.Seasonal.Points);
        Assert.Multiple(
            () => Assert.Equal(Domain.Seasonal, response.Domain),
            () => Assert.Equal(1.5, sample.TemperatureAnomaly),
            () => Assert.Equal(-12.0, sample.PrecipitationAnomaly),
            () => Assert.Equal(80.0, sample.PrecipitationMean));
    }

    private static readonly ImmutableArray<string> TwoModels = ["ecmwf_ifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"];

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_returns_the_requested_secondary_model_labelled_with_its_id()
    {
        var evt = new ForecastDataReceived(DomainData.Lucerne, DomainData.Forecast(Anchor, 2), "ukmo_global_ensemble_mean_20km");
        var service = CreateService(domainEvent: evt, models: TwoModels);

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Forecast, Model = "ukmo_global_ensemble_mean_20km" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal("ukmo_global_ensemble_mean_20km", response.Model),
            () => Assert.Equal(2, response.Forecast.Points.Count));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_labels_primary_weather_with_the_primary_model_id()
    {
        var evt = new ForecastDataReceived(DomainData.Lucerne, DomainData.Forecast(Anchor, 2));
        var service = CreateService(domainEvent: evt, models: TwoModels);

        var response = await service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Forecast },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal("ecmwf_ifs025_ensemble_mean", response.Model);
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_is_not_found_for_a_model_that_is_not_enabled()
    {
        var evt = new ForecastDataReceived(DomainData.Lucerne, DomainData.Forecast(Anchor, 2));
        var service = CreateService(domainEvent: evt);

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Forecast, Model = "ukmo_global_ensemble_mean_20km" },
            TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task GetCatalog_marks_only_the_effective_models_enabled()
    {
        var service = CreateService(models: TwoModels);

        var response = await service.GetCatalog(new GetCatalogRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(
            ["ecmwf_ifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"],
            response.Models.Where(m => m.Enabled).Select(m => m.Id).Order());
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_is_not_found_before_the_first_data_of_the_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne", Domain = Domain.Flood }, TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_rejects_an_unknown_location()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "atlantis", Domain = Domain.Forecast }, TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Multiple(
            () => Assert.Equal(StatusCode.NotFound, ex.StatusCode),
            () => Assert.Equal("Location 'atlantis' not found", ex.Status.Detail));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetDomainData_rejects_an_unspecified_domain()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetDomainData(
            new GetDomainDataRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken)));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task GetEnrichments_returns_the_alerts_the_consumer_holds()
    {
        var frost = new FrostAlertResult(DomainData.Lucerne, Messages.Alerts.AlertSeverity.Orange, 0.9, -3.0, 0.0, null, null, null,
            Messages.Alerts.ConfidenceLevel.High, -4.0, null);
        var service = CreateService(analysis: new LatestAnalysisResult(DomainData.Lucerne, [frost]));

        var response = await service.GetEnrichments(new GetEnrichmentsRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var evt = Assert.Single(response.Events);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", response.Location),
            () => Assert.Equal(EnrichmentEvent.FeatureOneofCase.Frost, evt.FeatureCase),
            () => Assert.Equal(V2.AlertSeverity.Orange, evt.Frost.Alert.Severity));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetEnrichments_returns_the_lifestyle_updates_the_consumer_holds()
    {
        var commute = new CommuteSafetyResult(DomainData.Lucerne, Messages.Pipeline.CommuteLevel.Caution, [Messages.Pipeline.CommuteHazard.Ice]);
        var service = CreateService(analysis: new LatestAnalysisResult(DomainData.Lucerne, [commute]));

        var response = await service.GetEnrichments(new GetEnrichmentsRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var evt = Assert.Single(response.Events);
        Assert.Multiple(
            () => Assert.Equal(EnrichmentEvent.FeatureOneofCase.CommuteSafety, evt.FeatureCase),
            () => Assert.Equal(V2.CommuteLevel.Caution, evt.CommuteSafety.Level),
            () => Assert.Equal([V2.CommuteHazard.Ice], evt.CommuteSafety.Hazards));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetEnrichments_returns_only_the_location_before_the_first_analysis()
    {
        var service = CreateService();

        var response = await service.GetEnrichments(new GetEnrichmentsRequest { Location = "lucerne" }, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(new GetEnrichmentsResponse { Location = "Lucerne" }, response);
    }

    private WeatherGrpcService CreateService(PipelineEvent? domainEvent = null, LatestAnalysisResult? analysis = null, ImmutableArray<string>? models = null)
    {
        ActorRegistry.Register<IGrpcConsumer>(
            Sys.ActorOf(Props.Create(() => new FakeConsumer(domainEvent, analysis))), overwrite: true);
        return new WeatherGrpcService(ActorRegistry, Sys, TestServiceInfo.Opts(Options),
            TestServiceInfo.Opts(new FeatureRequirementsOptions { WeatherModels = models ?? [ForecastOptions.DefaultModel] }), TimeProvider.System);
    }

    private sealed class FakeConsumer : ReceiveActor
    {
        public FakeConsumer(PipelineEvent? domainEvent, LatestAnalysisResult? analysis)
        {
            Receive<QueryDomainData>(msg => Sender.Tell(domainEvent is null || domainEvent.ToModelId() != msg.ModelId
                ? new DomainDataNotAvailable(msg.Location, msg.Domain, msg.ModelId)
                : new DomainDataResult(domainEvent)));
            Receive<QueryLatestAnalysis>(msg => Sender.Tell(
                analysis is null ? new LatestAnalysisNotAvailable(msg.Location) : analysis));
        }
    }
}
