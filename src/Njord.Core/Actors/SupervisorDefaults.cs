namespace Njord.Core.Actors;

public static class SupervisorDefaults
{
    public static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    public const double RandomFactor = 0.2;
}
