namespace Njord.Core.Configuration;

public sealed class LocationOptions
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool HasCoordinates => Latitude != 0 || Longitude != 0;

    public IList<string>? Models { get; set; }
}
