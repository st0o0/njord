using Njord.Weather;

namespace Njord.Core.Configuration;

public static class NjordOptionsExtensions
{
    public static LocationOptions? FindLocation(this NjordOptions options, string name) =>
        options.Locations.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
