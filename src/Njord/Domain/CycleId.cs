namespace Njord.Domain;

/// <summary>Identifies one poll cycle; derived from the tick timestamp.</summary>
public readonly record struct CycleId(DateTimeOffset Timestamp)
{
    public override string ToString() => Timestamp.ToString("O");
}
