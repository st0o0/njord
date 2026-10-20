using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;

namespace Njord.Marine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMarineDomain(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRule<DomainForecast<MarinePoint>>, SwimmingConditionsRule>();
        services.AddSingleton<IDomainHandler, SwimmingConditionsHandler>();
        return services;
    }
}
