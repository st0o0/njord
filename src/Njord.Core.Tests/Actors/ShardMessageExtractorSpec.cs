using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Core.Tests.Actors;

public sealed class ShardMessageExtractorSpec
{
    private static readonly PollTarget Lucerne = new(new LocationId("lucerne"), "icon_d2");

    private readonly ShardMessageExtractor _extractor = new();

    [Fact]
    public void A_poll_target_id_joins_location_and_model_with_a_pipe()
    {
        Assert.Multiple(
            () => Assert.Equal("lucerne|icon_d2", Lucerne.Id),
            () => Assert.Equal("lucerne/icon_d2", Lucerne.ToString()));
    }

    [Fact]
    public void Every_polling_message_is_routed_by_its_target_id()
    {
        Assert.Multiple(
            () => Assert.Equal("lucerne|icon_d2", _extractor.EntityId(new StartPolling(Lucerne, TimeSpan.FromMinutes(20)))),
            () => Assert.Equal("lucerne|icon_d2", _extractor.EntityId(new StopPolling("lucerne|icon_d2"))),
            () => Assert.Equal("lucerne|icon_d2", _extractor.EntityId(new TriggerTargetPoll("lucerne|icon_d2"))));
    }

    [Fact]
    public void Messages_of_the_same_target_land_in_the_same_shard()
    {
        var startMessage = new StartPolling(Lucerne, TimeSpan.FromMinutes(20));
        var triggerMessage = new TriggerTargetPoll(Lucerne.Id);

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
