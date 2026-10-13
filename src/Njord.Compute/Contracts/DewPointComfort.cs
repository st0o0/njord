namespace Njord.Compute.Contracts;

public enum DewPointComfort
{
    Dry,
    Comfortable,
    Sticky,
    Oppressive,
    Dangerous,
}

public static class DewPointComfortExtensions
{
    public static string ToWireName(this DewPointComfort comfort) => comfort switch
    {
        DewPointComfort.Dry => "dry",
        DewPointComfort.Comfortable => "comfortable",
        DewPointComfort.Sticky => "sticky",
        DewPointComfort.Oppressive => "oppressive",
        DewPointComfort.Dangerous => "dangerous",
        _ => throw new ArgumentOutOfRangeException(nameof(comfort), comfort, null),
    };
}
