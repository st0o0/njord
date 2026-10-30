using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Features;
using Njord.Messages.Results;
using Njord.OpenMeteo.Configuration;
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

        services.AddSingleton<IOptions<ActiveModels>>(sp =>
        {
            var forecast = sp.GetRequiredService<IOptions<ForecastOptions>>().Value;
            var ensemble = sp.GetRequiredService<IOptions<EnsembleOptions>>().Value;
            return Options.Create(new ActiveModels
            {
                PrimaryForecastModel = forecast.Models.Length > 0 ? forecast.Models[0] : ActiveModels.BestMatch,
                ForecastModels = [.. forecast.Models],
                EnsembleModels = [.. ensemble.Models],
            });
        });

        services.AddSingleton<IOptions<DomainCallConfig>>(sp =>
        {
            var forecast = sp.GetRequiredService<IOptions<ForecastOptions>>().Value;
            var ensemble = sp.GetRequiredService<IOptions<EnsembleOptions>>().Value;
            var flood = sp.GetRequiredService<IOptions<FloodOptions>>().Value;

            return Options.Create(new DomainCallConfig(
                ForecastDays: forecast.ForecastDays,
                ForecastModelCount: forecast.Models.Length,
                EnsembleDays: ensemble.ForecastDays,
                EnsembleModelCount: ensemble.Models.Length,
                AirQualityDays: sp.GetRequiredService<IOptions<AirQualityOptions>>().Value.ForecastDays,
                FloodDays: flood.ForecastDays,
                FloodIncludeEnsemble: flood.IncludeEnsemble,
                MarineDays: sp.GetRequiredService<IOptions<MarineOptions>>().Value.ForecastDays,
                SatellitePastHours: sp.GetRequiredService<IOptions<SatelliteOptions>>().Value.PastHours,
                SeasonalMonths: sp.GetRequiredService<IOptions<SeasonalOptions>>().Value.ForecastMonths));
        });
    }
}
