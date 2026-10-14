using AkkaLevel = Akka.Event.LogLevel;

namespace Njord.Configuration;

// The actor system only creates log events at or above akka.loglevel; Serilog then filters per category.
// The Akka level is the lowest level the Serilog configuration could let through anywhere, so no event that
// some category would show is dropped early and no Debug event is created when nothing shows Debug.
internal static class AkkaLogLevel
{
    private const string MinimumLevelSection = "Serilog:MinimumLevel";

    public static AkkaLevel From(IConfiguration configuration)
    {
        var section = configuration.GetSection(MinimumLevelSection);
        var defaultLevel = Parse(section.Value) ?? Parse(section["Default"]) ?? AkkaLevel.InfoLevel;
        var overrides = section.GetSection("Override").GetChildren().Select(o => Parse(o.Value)).OfType<AkkaLevel>();

        return overrides.Append(defaultLevel).Min();
    }

    private static AkkaLevel? Parse(string? serilogLevel) => serilogLevel?.Trim().ToLowerInvariant() switch
    {
        "verbose" or "debug" => AkkaLevel.DebugLevel,
        "information" => AkkaLevel.InfoLevel,
        "warning" => AkkaLevel.WarningLevel,
        "error" or "fatal" => AkkaLevel.ErrorLevel,
        _ => null,
    };
}
