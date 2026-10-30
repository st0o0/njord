using Njord.Core;
using Njord.Core.Configuration;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions, NjordOptionsValidator>();
        services.PostConfigure<NjordOptions>(o =>
            o.ResolvedLocations = o.Locations
                .Select(l => new Location(l.Name, l.Latitude.GetValueOrDefault(), l.Longitude.GetValueOrDefault()))
                .ToList());
        services.AddNjordOptions<PersistenceOptions>();
    }
}
