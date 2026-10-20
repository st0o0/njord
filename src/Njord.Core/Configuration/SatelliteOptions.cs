namespace Njord.Core.Configuration;

public sealed class SatelliteOptions
{
    public const string SectionName = "Njord:Satellite";

    // Hours of observed radiation requested per fetch; the blend only needs the recent hours.
    public int PastHours { get; set; } = 6;
}
