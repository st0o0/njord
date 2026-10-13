namespace Njord.Core.Configuration;

public interface IConfigWriter
{
    Task<ConfigUpdateResult<T>> UpdateAsync<T>(Action<T> applyChanges, CancellationToken cancellationToken)
        where T : class, new();

    IReadOnlyList<string> Check<T>(Action<T> applyChanges)
        where T : class, new();
}

public sealed record ConfigUpdateResult<T>(
    bool Applied,
    T Value,
    IReadOnlyList<string> ChangedProperties,
    IReadOnlyList<string> Failures)
    where T : class
{
    public bool RequiresRestart =>
        Applied && ChangedProperties.Any(p => ConfigReloadPolicy.For(typeof(T), p) == ReloadMode.RestartRequired);
}
