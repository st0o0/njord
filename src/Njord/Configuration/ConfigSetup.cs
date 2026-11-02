using Njord.Core;
using Njord.Core.Configuration;
using Terp.Hosting;

namespace Njord.Configuration;

public sealed class ConfigSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddNjordOptions<NjordOptions, NjordOptionsValidator>();
        services.PostConfigure<NjordOptions>(o =>
            o.ResolvedLocations =
            [
                .. o.Locations
                    .Select(l => new Location(l.Name, l.Latitude.GetValueOrDefault(), l.Longitude.GetValueOrDefault()))
            ]);
        services.AddNjordOptions<PersistenceOptions>();
    }
}
