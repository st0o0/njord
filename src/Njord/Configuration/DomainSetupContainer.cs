using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Features;
using Njord.Features.Conditions.SolarYield;
using Njord.Messages.Results;
using Njord.OpenMeteo.Configuration;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFeatures();

        services.AddOptions<SolarYieldConfig>().PostConfigure<IOptions<SolarYieldOptions>>((config, solar) =>
        {
            config.PeakKw = solar.Value.PeakKw;
            config.SystemEfficiency = solar.Value.SystemEfficiency;
        });

        services.AddSingleton<IOptions<DomainRequirements>>(sp =>
            Options.Create(
                sp.GetServices<IFeatureRule>()
                    .Where(r => r.Enabled)
                    .Aggregate(new DomainRequirements(), (all, f) => all | f.Descriptor.Requirements)
                    .WithBaseParams()));
    }
}
