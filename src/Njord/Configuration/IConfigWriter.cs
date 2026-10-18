namespace Njord.Configuration;

public interface IConfigWriter
{
    Task<ConfigUpdateResult<T>> UpdateAsync<T>(Action<T> applyChanges, CancellationToken cancellationToken)
        where T : class, new();

    // Validates every section first and persists all of them with one write and one reload, or none of them.
    // A dry run validates without persisting. A reset section falls back to the base configuration before its
    // change is applied.
    Task<ConfigBatchResult> UpdateManyAsync(
        IReadOnlyList<SectionUpdate> updates, bool dryRun, CancellationToken cancellationToken);

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

public abstract class SectionUpdate
{
    internal SectionUpdate()
    {
    }

    public abstract Type OptionsType { get; }

    public static SectionUpdate For<T>(Action<T> applyChanges, bool reset = false)
        where T : class, new() =>
        new SectionUpdate<T>(applyChanges, reset);

    internal abstract ConfigWriter.SectionPlan Plan(ConfigWriter writer);
}

public sealed class SectionUpdate<T> : SectionUpdate
    where T : class, new()
{
    private readonly Action<T> _applyChanges;
    private readonly bool _reset;

    internal SectionUpdate(Action<T> applyChanges, bool reset)
    {
        _applyChanges = applyChanges;
        _reset = reset;
    }

    public override Type OptionsType => typeof(T);

    internal override ConfigWriter.SectionPlan Plan(ConfigWriter writer) => writer.Plan(_applyChanges, _reset);
}

public sealed record SectionOutcome(
    Type OptionsType,
    IReadOnlyList<string> ChangedProperties,
    IReadOnlyList<string> Failures)
{
    public IEnumerable<string> RestartRequiredProperties =>
        ChangedProperties.Where(p => ConfigReloadPolicy.For(OptionsType, p) == ReloadMode.RestartRequired);
}

public sealed record ConfigBatchResult(bool Applied, IReadOnlyList<SectionOutcome> Sections)
{
    public bool Valid => Sections.All(s => s.Failures.Count == 0);
}
