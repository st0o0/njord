using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Core.Tests.Actors;

public sealed class ShardMessageExtractorSpec
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
