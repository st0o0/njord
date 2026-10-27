namespace Njord.Core;

public readonly record struct Location(string Name, double Latitude, double Longitude)
{
    public bool Equals(Location other) =>
        string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        Name is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString() => $"{Name} ({Latitude}, {Longitude})";
}
