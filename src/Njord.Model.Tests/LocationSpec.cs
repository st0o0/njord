namespace Njord.Model.Tests;

public sealed class LocationSpec
{
    [Fact]
    public void Locations_with_different_case_are_equal()
    {
        var a = new Location("Lucerne", 47.05, 8.31);
        var b = new Location("lucerne", 47.05, 8.31);

        Assert.Multiple(
            () => Assert.Equal(a, b),
            () => Assert.Equal(a.GetHashCode(), b.GetHashCode()));
    }

    [Fact]
    public void Same_name_with_different_coordinates_is_equal()
    {
        var a = new Location("Lucerne", 47.05, 8.31);
        var b = new Location("Lucerne", 1.0, 2.0);

        Assert.Multiple(
            () => Assert.Equal(a, b),
            () => Assert.Equal(a.GetHashCode(), b.GetHashCode()));
    }

    [Fact]
    public void Different_names_are_not_equal()
    {
        Assert.NotEqual(new Location("Lucerne", 47.05, 8.31), new Location("Bern", 47.05, 8.31));
    }

    [Fact]
    public void Works_as_dictionary_key_regardless_of_case()
    {
        var map = new Dictionary<Location, int> { [new Location("Lucerne", 47.05, 8.31)] = 1 };

        Assert.Equal(1, map[new Location("LUCERNE", 0, 0)]);
    }

    [Fact]
    public void To_string_shows_name_and_coordinates()
    {
        var location = new Location("Lucerne", 47.05, 8.31);

        Assert.Equal(string.Create(System.Globalization.CultureInfo.CurrentCulture, $"Lucerne ({47.05}, {8.31})"), location.ToString());
    }

    [Fact]
    public void Keeps_coordinates_as_payload()
    {
        var location = new Location("Lucerne", 47.05, 8.31);

        Assert.Multiple(
            () => Assert.Equal("Lucerne", location.Name),
            () => Assert.Equal(47.05, location.Latitude),
            () => Assert.Equal(8.31, location.Longitude));
    }
}
