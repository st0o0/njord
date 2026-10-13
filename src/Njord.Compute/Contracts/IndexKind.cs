namespace Njord.Compute.Contracts;

public enum IndexKind
{
    Laundry,
    Outdoor,
    Running,
    Cycling,
    Bbq,
    Irrigation,
    Solar,
    NightVentilation,
}

public static class IndexKinds
{
    public static readonly IReadOnlyList<IndexKind> All =
    [
        IndexKind.Laundry,
        IndexKind.Outdoor,
        IndexKind.Running,
        IndexKind.Cycling,
        IndexKind.Bbq,
        IndexKind.Irrigation,
        IndexKind.Solar,
        IndexKind.NightVentilation,
    ];

    public static string ToWireName(this IndexKind kind) => kind switch
    {
        IndexKind.Laundry => "laundry",
        IndexKind.Outdoor => "outdoor",
        IndexKind.Running => "running",
        IndexKind.Cycling => "cycling",
        IndexKind.Bbq => "bbq",
        IndexKind.Irrigation => "irrigation",
        IndexKind.Solar => "solar",
        IndexKind.NightVentilation => "night_ventilation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string ToConfigName(this IndexKind kind) => kind switch
    {
        IndexKind.Laundry => "Laundry",
        IndexKind.Outdoor => "Outdoor",
        IndexKind.Running => "Running",
        IndexKind.Cycling => "Cycling",
        IndexKind.Bbq => "Bbq",
        IndexKind.Irrigation => "Irrigation",
        IndexKind.Solar => "Solar",
        IndexKind.NightVentilation => "NightVentilation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static bool TryParse(string? configKey, out IndexKind kind)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.ToConfigName(), configKey, StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}
