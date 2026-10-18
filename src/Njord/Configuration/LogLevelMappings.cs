using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Njord.Configuration;

internal static class LogLevelMappings
{
    public static MelLogLevel ToLogLevel(this Akka.Event.LogLevel level) => level switch
    {
        Akka.Event.LogLevel.DebugLevel => MelLogLevel.Debug,
        Akka.Event.LogLevel.InfoLevel => MelLogLevel.Information,
        Akka.Event.LogLevel.WarningLevel => MelLogLevel.Warning,
        Akka.Event.LogLevel.ErrorLevel => MelLogLevel.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
