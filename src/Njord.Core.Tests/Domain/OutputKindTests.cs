using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class OutputKindTests
{
    [Theory]
    [InlineData(nameof(OutputKind.Weather))]
    [InlineData(nameof(OutputKind.Observation))]
    public void New_output_kinds_exist(string name)
    {
        Assert.True(Enum.TryParse<OutputKind>(name, out _));
    }

    [Fact]
    public void All_output_kinds_are_present()
    {
        var values = Enum.GetValues<OutputKind>();

        Assert.Equal(7, values.Length);
    }
}
