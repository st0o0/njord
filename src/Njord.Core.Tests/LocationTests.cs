namespace Njord.Core.Tests;

public sealed class LocationTests
{
    [Fact]
    public void Should_treat_locations_with_different_name_casing_as_equal()
    {
        var a = new Location("Lucerne", 47.05, 8.31);
        var b = new Location("LUCERNE", 47.05, 8.31);

        Assert.Multiple(
            () => Assert.Equal(a, b),
            () => Assert.Equal(a.GetHashCode(), b.GetHashCode()));
    }

    [Fact]
    public void Should_treat_locations_with_different_names_as_different()
    {
        Assert.NotEqual(new Location("Lucerne", 47.05, 8.31), new Location("Zurich", 47.05, 8.31));
    }

    [Fact]
    public void Should_collapse_case_variants_in_a_hash_set()
    {
        var set = new HashSet<Location> { new("Bern", 46.9, 7.4), new("bern", 46.9, 7.4) };

        Assert.Single(set);
    }
}
