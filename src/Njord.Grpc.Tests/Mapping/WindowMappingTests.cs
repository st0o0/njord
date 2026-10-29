using Google.Protobuf.WellKnownTypes;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using DomainLimitingKind = Njord.Messages.Results.LimitingKind;
using ProtoLimitingKind = Njord.Grpc.V2.LimitingKind;

namespace Njord.Grpc.Tests.Mapping;

public sealed class WindowMappingTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activity_window_maps_to_time_window()
    {
        var window = new ActivityWindow(Start, Start.AddHours(3), 72,
            new LimitingFactor(DomainLimitingKind.Wind, Start.AddHours(3), 12.5));

        var proto = window.ToProto();

        Assert.NotNull(proto.Limiting);
        Assert.Multiple(
            () => Assert.Equal(Start.ToTimestamp(), proto.Start),
            () => Assert.Equal(Start.AddHours(3).ToTimestamp(), proto.End),
            () => Assert.Equal(72, proto.Score),
            () => Assert.Equal(ProtoLimitingKind.Wind, proto.Limiting.Kind));
    }

    [Fact]
    public void Window_without_limiting_factor_maps_to_null()
    {
        var proto = new ActivityWindow(Start, Start.AddHours(2), 60, null).ToProto();

        Assert.Null(proto.Limiting);
    }

    [Fact]
    public void Limiting_factor_maps_kind_time_and_value()
    {
        var proto = new LimitingFactor(DomainLimitingKind.Rain, Start, 4.2).ToProto();

        Assert.Multiple(
            () => Assert.Equal(ProtoLimitingKind.Rain, proto.Kind),
            () => Assert.Equal(Start.ToTimestamp(), proto.From),
            () => Assert.Equal(4.2, proto.Value));
    }

    [Theory]
    [InlineData(DomainLimitingKind.None, ProtoLimitingKind.Unspecified)]
    [InlineData(DomainLimitingKind.Rain, ProtoLimitingKind.Rain)]
    [InlineData(DomainLimitingKind.Uv, ProtoLimitingKind.Uv)]
    [InlineData(DomainLimitingKind.Wind, ProtoLimitingKind.Wind)]
    [InlineData(DomainLimitingKind.TemperatureHigh, ProtoLimitingKind.TemperatureHigh)]
    [InlineData(DomainLimitingKind.TemperatureLow, ProtoLimitingKind.TemperatureLow)]
    [InlineData(DomainLimitingKind.Humidity, ProtoLimitingKind.Humidity)]
    [InlineData(DomainLimitingKind.Pollen, ProtoLimitingKind.Pollen)]
    [InlineData(DomainLimitingKind.AirQuality, ProtoLimitingKind.AirQuality)]
    [InlineData(DomainLimitingKind.Visibility, ProtoLimitingKind.Visibility)]
    [InlineData(DomainLimitingKind.Thunderstorm, ProtoLimitingKind.Thunderstorm)]
    [InlineData(DomainLimitingKind.Darkness, ProtoLimitingKind.Darkness)]
    public void Limiting_kind_maps_to_proto(DomainLimitingKind kind, ProtoLimitingKind expected)
    {
        Assert.Equal(expected, kind.ToProto());
    }

    [Fact]
    public void Score_without_windows_maps_to_empty_repeated()
    {
        var result = new LaundryScoreResult(new Location("Home", 47.0, 8.0), 70, Start, 3);

        Assert.Empty(result.ToData().Windows);
    }

    [Fact]
    public void Score_windows_map_to_repeated_time_windows()
    {
        var result = new LaundryScoreResult(new Location("Home", 47.0, 8.0), 70, Start, 3)
        {
            Windows = [new ActivityWindow(Start, Start.AddHours(3), 70, null)],
        };

        var window = Assert.Single(result.ToData().Windows);
        Assert.Equal(70, window.Score);
    }
}
