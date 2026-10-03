namespace Njord.Domain.Weather;

public readonly record struct CycleId(DateTimeOffset Timestamp)
{
    public override string ToString() => Timestamp.ToString("O");
}
