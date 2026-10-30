using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Features;
using Njord.OpenMeteo.Configuration;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFeatures();

        services.AddOptions<ActiveModels>()
            .PostConfigure<IOptions<ForecastOptions>, IOptions<EnsembleOptions>>((models, forecast, ensemble) =>
            {
                var f = forecast.Value;
                var e = ensemble.Value;
                models.PrimaryForecastModel = f.Models.Length > 0 ? f.Models[0] : ActiveModels.BestMatch;
                models.ForecastModels = [.. f.Models];
                models.EnsembleModels = [.. e.Models];
            });

        services.AddOptions<DomainCallConfig>();
        services.AddSingleton<IConfigureOptions<DomainCallConfig>, ConfigureDomainCallConfig>();
    }
}
