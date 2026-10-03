using Akka.Actor;

namespace Njord.Actors;

public static class RetryBackoff
{
    public const string OverridePath = "njord.retry-backoff.override";

    private const double MaxSeconds = 30;

    public static TimeSpan Default(int attempt)
        => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), MaxSeconds));

    public static TimeSpan For(ActorSystem system, int attempt)
    {
        var config = system.Settings.Config;
        return config.HasPath(OverridePath)
            ? config.GetTimeSpan(OverridePath)
            : Default(attempt);
    }
}
