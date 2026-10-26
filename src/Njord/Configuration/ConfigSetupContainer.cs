using Njord.Core.Configuration;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions, NjordOptionsValidator>();
        services.AddNjordOptions<PersistenceOptions, PersistenceOptionsValidator>();
    }
}
