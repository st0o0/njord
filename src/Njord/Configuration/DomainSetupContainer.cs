using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features;
using Njord.Messages.Results;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFeatures();

        services.AddSingleton<IOptions<DomainRequirements>>(sp =>
            Options.Create(
                sp.GetServices<IFeatureRule>()
                    .Where(r => r.Enabled)
                    .Aggregate(new DomainRequirements(), (all, f) => all | f.Descriptor.Requirements)
                    .WithBaseParams()));
    }
}
