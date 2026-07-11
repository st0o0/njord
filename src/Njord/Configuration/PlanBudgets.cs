namespace Njord.Configuration;

public static class PlanBudgets
{
    // Only Hobby is verified against the API docs (2026-07-11). The Business
    // presets are conservative placeholders until verified — use Custom with a
    // budget override for exact contract limits.
    private static readonly Dictionary<NjordPlan, RequestBudget> Presets = new()
    {
        [NjordPlan.Hobby] = new RequestBudget(20_000, 60),
        [NjordPlan.BusinessStarter] = new RequestBudget(50_000, 60),
        [NjordPlan.BusinessStandard] = new RequestBudget(100_000, 120),
        [NjordPlan.BusinessProfessional] = new RequestBudget(250_000, 240),
        [NjordPlan.BusinessEnterprise] = new RequestBudget(500_000, 600),
    };

    /// <summary>Resolves the effective budget; null when <see cref="NjordPlan.Custom"/> lacks an override.</summary>
    public static RequestBudget? Resolve(NjordPlan plan, RequestBudget? overrideBudget)
        => overrideBudget ?? Presets.GetValueOrDefault(plan);
}
