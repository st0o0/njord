using Njord.Core;
using Njord.Messages.Results;

namespace Njord.Messages.Tests.Results;

public sealed class ObservationResultTests
{
    private static readonly Location _vienna = new("_vienna", 48.2, 16.37);

    [Fact]
    public void Construction_with_value()
    {
        var result = new ObservationResult(_vienna, "TestFeature", 42.5);

        Assert.Equal(_vienna, result.Location);
        Assert.Equal("TestFeature", result.FeatureName);
        Assert.Equal(42.5, result.Value);
    }

    [Fact]
    public void Is_a_feature_result()
    {
        var result = new ObservationResult(_vienna, "TestFeature", 12.3);

        Assert.IsAssignableFrom<FeatureResult>(result);
    }

    [Fact]
    public void Record_equality()
    {
        var a = new ObservationResult(_vienna, "TestFeature", 42.5);
        var b = new ObservationResult(_vienna, "TestFeature", 42.5);

        Assert.Equal(a, b);
    }
}
