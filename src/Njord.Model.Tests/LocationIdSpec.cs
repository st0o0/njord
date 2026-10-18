namespace Njord.Model.Tests;

public sealed class LocationIdSpec
{
    [Fact]
    public void Location_ids_with_different_case_are_equal()
    {
        var a = new LocationId("Lucerne");
        var b = new LocationId("lucerne");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_names_are_not_equal()
    {
        Assert.NotEqual(new LocationId("Lucerne"), new LocationId("Bern"));
    }

    [Fact]
    public void Name_keeps_the_configured_spelling()
    {
        var id = new LocationId("Lucerne");

        Assert.Equal("Lucerne", id.Name);
        Assert.Equal("Lucerne", id.ToString());
    }

    [Fact]
    public void Location_id_works_as_dictionary_key_regardless_of_case()
    {
        var map = new Dictionary<LocationId, int> { [new LocationId("Lucerne")] = 1 };

        Assert.Equal(1, map[new LocationId("LUCERNE")]);
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new LocationId(" "));
    }
}
