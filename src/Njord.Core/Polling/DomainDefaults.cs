namespace Njord.Core.Polling;

public static class DomainDefaults
{
    public static TimeSpan PollInterval(DomainKind domain) => domain switch
    {
        DomainKind.Forecast => TimeSpan.FromMinutes(60),
        DomainKind.AirQuality => TimeSpan.FromMinutes(60),
        DomainKind.Marine => TimeSpan.FromHours(6),
        DomainKind.Ensemble => TimeSpan.FromHours(6),
        DomainKind.Seasonal => TimeSpan.FromHours(24),
        DomainKind.Flood => TimeSpan.FromHours(24),
        _ => TimeSpan.FromMinutes(60),
    };
}
