using Njord.Forecast.Weather;

namespace Njord.Core.Domain;

public static class WeatherParamExtensions
{
    public static int FlagCount(this WeatherParam flags) =>
        System.Numerics.BitOperations.PopCount((ulong)flags);
}
