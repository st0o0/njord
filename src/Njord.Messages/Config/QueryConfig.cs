namespace Njord.Messages.Config;

public sealed record QueryConfig;

public abstract record ConfigResponse;
public sealed record ConfigResult(EffectiveConfig Config) : ConfigResponse;
public sealed record ConfigFailed(Exception Cause) : ConfigResponse;
