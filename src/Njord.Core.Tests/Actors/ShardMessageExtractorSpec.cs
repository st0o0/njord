using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Core.Tests.Actors;

public sealed class ShardMessageExtractorSpec
{
    private static readonly PollTarget Lucerne = new(new LocationId("lucerne"));

    private readonly ShardMessageExtractor _extractor = new();

    [Fact]
    public void A_poll_target_id_is_the_location_name()
    {
        Assert.Multiple(
            () => Assert.Equal("lucerne", Lucerne.Id),
            () => Assert.Equal("lucerne", Lucerne.ToString()));
    }

    [Fact]
    public void Every_polling_message_is_routed_by_its_location_name()
    {
        Assert.Multiple(
            () => Assert.Equal("lucerne", _extractor.EntityId(new StartPolling(Lucerne, TimeSpan.FromMinutes(20)))),
            () => Assert.Equal("lucerne", _extractor.EntityId(new StopPolling("lucerne"))),
            () => Assert.Equal("lucerne", _extractor.EntityId(new TriggerLocationPoll("lucerne"))));
    }

    [Fact]
    public void Messages_of_the_same_target_land_in_the_same_shard()
    {
        var startMessage = new StartPolling(Lucerne, TimeSpan.FromMinutes(20));
        var triggerMessage = new TriggerLocationPoll(Lucerne.Id);

        var start = _extractor.ShardId(_extractor.EntityId(startMessage), startMessage);
        var trigger = _extractor.ShardId(_extractor.EntityId(triggerMessage), triggerMessage);

        Assert.Equal(start, trigger);
    }

    [Fact]
    public void An_unknown_message_is_rejected_with_its_type_name()
    {
        var ex = Assert.Throws<ArgumentException>(() => _extractor.EntityId("not sharded"));

        Assert.Multiple(
            () => Assert.Equal("message", ex.ParamName),
            () => Assert.Contains("String", ex.Message));
    }
}
