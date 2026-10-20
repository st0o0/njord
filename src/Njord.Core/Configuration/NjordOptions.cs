namespace Njord.Core.Configuration;

public sealed class NjordOptions
{
    public const string SectionName = "Njord";

    public IList<LocationOptions> Locations { get; set; } = [];
}
