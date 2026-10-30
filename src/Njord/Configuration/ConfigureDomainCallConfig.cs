using Microsoft.Extensions.Options;
using Njord.Core.Polling;
using Njord.OpenMeteo.Configuration;

namespace Njord.Configuration;

internal sealed class ConfigureDomainCallConfig(
    IOptions<ForecastOptions> forecast,
    IOptions<EnsembleOptions> ensemble,
    IOptions<AirQualityOptions> airQuality,
    IOptions<FloodOptions> flood,
    IOptions<MarineOptions> marine,
    IOptions<SeasonalOptions> seasonal) : IConfigureOptions<DomainCallConfig>
{
    public void Configure(DomainCallConfig config)
    {
        var f = forecast.Value;
        var e = ensemble.Value;
        var fl = flood.Value;

        config.ForecastDays = f.ForecastDays;
        config.ForecastModelCount = f.Models.Length;
        config.EnsembleDays = e.ForecastDays;
        config.EnsembleModelCount = e.Models.Length;
        config.AirQualityDays = airQuality.Value.ForecastDays;
        config.FloodDays = fl.ForecastDays;
        config.FloodIncludeEnsemble = fl.IncludeEnsemble;
        config.MarineDays = marine.Value.ForecastDays;
        config.SeasonalMonths = seasonal.Value.ForecastMonths;
    }
}
