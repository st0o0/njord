using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results.Scores;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ScoreValueMappingTests
{
    [Fact]
    public void Score_best_start_and_hours_map()
    {
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var result = new BbqScoreResult(new Location("Vienna", 48.2, 16.37), 82, start, 4);

        var value = result.ToScoreValue();

        Assert.Multiple(
            () => Assert.Equal(82, value.Score),
            () => Assert.Equal(start, value.BestStart.ToDateTimeOffset()),
            () => Assert.Equal(4, value.BestHours));
    }
}
