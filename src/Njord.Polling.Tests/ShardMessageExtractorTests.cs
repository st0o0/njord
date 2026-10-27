using Njord.Core;
using Njord.Core.Domain;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Polling.Tests;

public sealed class ShardMessageExtractorTests
{
    private readonly ShardMessageExtractor _extractor = new();

    [Fact]
    public void Should_route_polling_messages_by_the_configured_location_name()
    {
        var target = new PollTarget(new Location("Lucerne", 0, 0));

        Assert.Multiple(
            () => Assert.Equal("Lucerne", _extractor.EntityId(new StartPolling(target, TimeSpan.FromMinutes(20)))),
            () => Assert.Equal("Lucerne", _extractor.EntityId(new StopPolling("Lucerne"))),
            () => Assert.Equal("Lucerne", _extractor.EntityId(new TriggerLocationPoll("Lucerne"))));
    }

    [Fact]
    public void Should_route_gateway_replies_by_the_location_name()
    {
        var location = new Location("Lucerne", 0, 0);

        Assert.Multiple(
            () => Assert.Equal("Lucerne", _extractor.EntityId(new FetchDomainCompleted(location, DomainKind.Forecast, 1))),
            () => Assert.Equal("Lucerne", _extractor.EntityId(new FetchDomainFailed(location, DomainKind.Forecast, PollFailureReason.Transport, null))),
            () => Assert.Equal("Lucerne", _extractor.EntityId(new FetchDomainRejected(location, DomainKind.Forecast, FetchRejectionReason.Budget))));
    }

    [Fact]
    public void Should_send_the_same_entity_to_the_same_shard()
    {
        Assert.Equal(
            _extractor.ShardId("Lucerne", new StopPolling("Lucerne")),
            _extractor.ShardId("Lucerne", new TriggerLocationPoll("Lucerne")));
    }

    [Fact]
    public void Should_reject_messages_without_a_location_id()
    {
        var ex = Assert.Throws<ArgumentException>(() => _extractor.EntityId("not routable"));

        Assert.Contains("String", ex.Message);
    }
}
