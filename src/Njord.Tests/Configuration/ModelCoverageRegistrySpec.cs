using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class ModelCoverageRegistrySpec
{
    [Fact(Timeout = 5000)]
    public void Global_model_returns_global_tier()
    {
        var coverage = ModelCoverageRegistry.Get("icon_global");

        Assert.NotNull(coverage);
        Assert.Equal(CoverageTier.Global, coverage!.Tier);
        Assert.Null(coverage.Bounds);
    }

    [Fact(Timeout = 5000)]
    public void Regional_model_returns_correct_bounding_box()
    {
        var coverage = ModelCoverageRegistry.Get("icon_d2");

        Assert.NotNull(coverage);
        Assert.Equal(CoverageTier.Regional, coverage!.Tier);
        Assert.NotNull(coverage.Bounds);
        Assert.True(coverage.Bounds!.Contains(51.84, 13.41));
    }

    [Fact(Timeout = 5000)]
    public void Unknown_model_returns_null()
    {
        Assert.Null(ModelCoverageRegistry.Get("future_model_2030"));
    }

    [Fact(Timeout = 5000)]
    public void BoundingBox_contains_inside_point()
    {
        var box = new BoundingBox(43, 57, 1, 18);

        Assert.True(box.Contains(50.0, 10.0));
    }

    [Fact(Timeout = 5000)]
    public void BoundingBox_rejects_outside_point()
    {
        var box = new BoundingBox(43, 57, 1, 18);

        Assert.False(box.Contains(60.0, 10.0));
        Assert.False(box.Contains(50.0, 25.0));
    }

    [Fact(Timeout = 5000)]
    public void BoundingBox_includes_boundary_point()
    {
        var box = new BoundingBox(43, 57, 1, 18);

        Assert.True(box.Contains(43.0, 1.0));
        Assert.True(box.Contains(57.0, 18.0));
    }

    [Fact(Timeout = 5000)]
    public void IsPlausible_returns_true_for_global_model_anywhere()
    {
        Assert.True(ModelCoverageRegistry.IsPlausible("icon_global", -33.0, 151.0));
    }

    [Fact(Timeout = 5000)]
    public void IsPlausible_returns_true_for_regional_model_in_coverage()
    {
        Assert.True(ModelCoverageRegistry.IsPlausible("icon_d2", 51.84, 13.41));
    }

    [Fact(Timeout = 5000)]
    public void IsPlausible_returns_false_for_regional_model_outside_coverage()
    {
        Assert.False(ModelCoverageRegistry.IsPlausible("knmi_harmonie_arome_netherlands", 51.84, 13.41));
    }

    [Fact(Timeout = 5000)]
    public void IsPlausible_returns_true_for_unknown_model()
    {
        Assert.True(ModelCoverageRegistry.IsPlausible("future_model", 0, 0));
    }

    [Fact(Timeout = 5000)]
    public void Lookup_is_case_insensitive()
    {
        Assert.NotNull(ModelCoverageRegistry.Get("ICON_D2"));
        Assert.NotNull(ModelCoverageRegistry.Get("Icon_Global"));
    }
}
