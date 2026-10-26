using System.Numerics;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;

namespace Njord.Tests.Shared;

public static class TestFetchPlan
{
    public static FetchPlan For(DomainRequirements requirements, EffectiveWeatherModels? models = null, int forecastDays = 4)
    {
        models ??= EffectiveWeatherModels.Default;
        var weight = requirements.Requires(DomainKind.Weather)
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllWeather), forecastDays) * models.Count
            : 0;
        return new FetchPlan(
            Enum.GetValues<DomainKind>().Where(requirements.Requires).ToHashSet(), requirements, models, weight);
    }

    public static FetchPlan All { get; } = For(TestRequirements.All);
}
