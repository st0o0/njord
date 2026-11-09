using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results.Conditions;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ConditionValueMappingTests
{
    [Fact]
    public void Condition_result_is_active()
    {
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var result = new VentilationWindowResult(new Location("Vienna", 48.2, 16.37), start, start.AddHours(2));

        Assert.True(result.ToConditionValue().Active);
    }
}
