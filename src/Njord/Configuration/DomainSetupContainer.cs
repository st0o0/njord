using Microsoft.Extensions.Options;
using Njord.Advisories;
using Njord.Alerts;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Insights;
using Njord.Scores;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAlertOptions(configuration);
        services.AddAlertHandlers().AddScoreHandlers().AddInsightHandlers().AddAdvisoryHandlers();

        services.AddSingleton<IReadOnlySet<DomainKind>>(static sp => FeatureResolution.RequiredDomains(
            sp.GetRequiredService<IOptions<FeatureOptions>>().Value,
            sp.GetServices<IDomainHandler>()));

        services.AddSingleton(static sp => new ResolvedRequirements(FeatureResolution.RequiredParams(
            sp.GetRequiredService<IOptions<FeatureOptions>>().Value,
            sp.GetServices<IDomainHandler>())));
    }
}
