namespace Njord.Compute.Contracts;

public enum VpdCategory
{
    Low,
    Optimal,
    High,
    Critical,
}

public static class VpdCategoryExtensions
{
    public static string ToWireName(this VpdCategory category) => category switch
    {
        VpdCategory.Low => "low",
        VpdCategory.Optimal => "optimal",
        VpdCategory.High => "high",
        VpdCategory.Critical => "critical",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
