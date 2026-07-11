namespace Njord.Configuration;

/// <summary>Root options bound from the <c>Njord</c> configuration section.</summary>
public sealed class NjordOptions
{
    public const string SectionName = "Njord";

    /// <summary>Bound from env var <c>Njord__ApiKey</c>; never stored in the repo.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public NjordPlan Plan { get; set; } = NjordPlan.Hobby;

    /// <summary>Replaces the plan preset entirely when set. Required for <see cref="NjordPlan.Custom"/>.</summary>
    public RequestBudget? BudgetOverride { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(60);

    public IList<LocationOptions> Locations { get; set; } = [];

    /// <summary>Kachelmann model ids (free-form strings, e.g. "ICON-D2").</summary>
    public IList<string> Models { get; set; } = [];
}
