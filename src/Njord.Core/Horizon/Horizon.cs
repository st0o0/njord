using System.Globalization;

namespace Njord.Core;

public readonly record struct Horizon
{
    private const char HourlyPrefix = 'h';
    private const char DailyPrefix = 'd';

    private Horizon(HorizonKind kind, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Kind = kind;
        Offset = offset;
    }

    public HorizonKind Kind { get; }

    public int Offset { get; }

    public bool IsHourly => Kind == HorizonKind.Hourly;

    public static Horizon Hours(int hours) => new(HorizonKind.Hourly, hours);

    public static Horizon Days(int days) => new(HorizonKind.Daily, days);

    public string ToWireKey() =>
        $"{(IsHourly ? HourlyPrefix : DailyPrefix)}{Offset.ToString(CultureInfo.InvariantCulture)}";

    public static bool TryParseWireKey(string? key, out Horizon horizon)
    {
        horizon = default;
        if (key is null || key.Length < 2)
        {
            return false;
        }

        HorizonKind? kind = key[0] switch
        {
            HourlyPrefix => HorizonKind.Hourly,
            DailyPrefix => HorizonKind.Daily,
            _ => null,
        };

        if (kind is null
            || !int.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            return false;
        }

        horizon = new Horizon(kind.Value, offset);
        return true;
    }

    public override string ToString() => ToWireKey();
}
