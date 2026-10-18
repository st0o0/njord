using Njord.Configuration;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Njord.Tests.Logging;

public sealed class LogLevelMappingsSpec
{
    [Theory]
    [InlineData(Akka.Event.LogLevel.DebugLevel, MelLogLevel.Debug)]
    [InlineData(Akka.Event.LogLevel.InfoLevel, MelLogLevel.Information)]
    [InlineData(Akka.Event.LogLevel.WarningLevel, MelLogLevel.Warning)]
    [InlineData(Akka.Event.LogLevel.ErrorLevel, MelLogLevel.Error)]
    public void Every_akka_level_maps_to_its_logging_level(Akka.Event.LogLevel akka, MelLogLevel expected)
    {
        Assert.Equal(expected, akka.ToLogLevel());
    }

    [Fact]
    public void An_undefined_akka_level_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((Akka.Event.LogLevel)99).ToLogLevel());

        Assert.Equal("level", ex.ParamName);
        Assert.Equal((Akka.Event.LogLevel)99, ex.ActualValue);
    }
}
