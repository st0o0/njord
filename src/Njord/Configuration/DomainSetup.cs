using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Features;
using Njord.OpenMeteo.Configuration;
using Terp.Hosting;

namespace Njord.Configuration;

public sealed class DomainSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
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
