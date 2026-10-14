using Microsoft.Extensions.Configuration;
using Njord.Configuration;
using AkkaLevel = Akka.Event.LogLevel;

namespace Njord.Tests.Logging;

public sealed class AkkaLogLevelSpec
{
    private static AkkaLevel LevelFor(params (string Key, string Value)[] values) =>
        AkkaLogLevel.From(new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build());

    [Theory]
    [InlineData("Verbose", AkkaLevel.DebugLevel)]
    [InlineData("Debug", AkkaLevel.DebugLevel)]
    [InlineData("Information", AkkaLevel.InfoLevel)]
    [InlineData("Warning", AkkaLevel.WarningLevel)]
    [InlineData("Error", AkkaLevel.ErrorLevel)]
    [InlineData("Fatal", AkkaLevel.ErrorLevel)]
    public void The_default_minimum_level_maps_to_the_akka_level(string level, AkkaLevel expected) =>
        Assert.Equal(expected, LevelFor(("Serilog:MinimumLevel:Default", level)));

    [Fact]
    public void A_minimum_level_given_as_a_single_value_maps_to_the_akka_level() =>
        Assert.Equal(AkkaLevel.WarningLevel, LevelFor(("Serilog:MinimumLevel", "Warning")));

    [Fact]
    public void Production_settings_keep_akka_at_info() =>
        Assert.Equal(AkkaLevel.InfoLevel, LevelFor(
            ("Serilog:MinimumLevel:Default", "Information"),
            ("Serilog:MinimumLevel:Override:Akka", "Warning"),
            ("Serilog:MinimumLevel:Override:Microsoft.AspNetCore", "Warning")));

    [Fact]
    public void A_debug_override_lowers_akka_to_debug() =>
        Assert.Equal(AkkaLevel.DebugLevel, LevelFor(
            ("Serilog:MinimumLevel:Default", "Information"),
            ("Serilog:MinimumLevel:Override:Akka", "Warning"),
            ("Serilog:MinimumLevel:Override:Njord.Pipeline", "Debug")));

    [Fact]
    public void Overrides_above_the_default_do_not_raise_the_akka_level() =>
        Assert.Equal(AkkaLevel.WarningLevel, LevelFor(
            ("Serilog:MinimumLevel:Default", "Warning"),
            ("Serilog:MinimumLevel:Override:Akka", "Error")));

    [Fact]
    public void Overrides_without_a_default_count_against_the_serilog_default_of_information() =>
        Assert.Equal(AkkaLevel.InfoLevel, LevelFor(("Serilog:MinimumLevel:Override:Akka", "Warning")));

    [Fact]
    public void A_missing_minimum_level_keeps_akka_at_info() =>
        Assert.Equal(AkkaLevel.InfoLevel, LevelFor());

    [Fact]
    public void An_unparseable_level_is_ignored() =>
        Assert.Equal(AkkaLevel.WarningLevel, LevelFor(
            ("Serilog:MinimumLevel:Default", "Warning"),
            ("Serilog:MinimumLevel:Override:Njord", "Chatty")));
}
