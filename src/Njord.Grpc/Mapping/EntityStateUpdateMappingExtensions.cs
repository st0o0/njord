using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class EntityStateUpdateMappingExtensions
{
    public static Proto.EntityStateUpdate ToEntityStateUpdate(this FeatureResult result, string entityId, DateTimeOffset updatedAt)
    {
        var update = new Proto.EntityStateUpdate
        {
            EntityId = entityId,
            UpdatedAt = Timestamp.FromDateTimeOffset(updatedAt),
        };
        switch (result)
        {
            case AlertFeatureResult alert:
                update.Alert = alert.ToAlertValue();
                break;
            case ScoreFeatureResult score:
                update.Score = score.ToScoreValue();
                break;
            case ConditionFeatureResult condition:
                update.Condition = condition.ToConditionValue();
                break;
            case TrendFeatureResult trend:
                update.Trend = trend.ToTrendValue();
                break;
            case ForecastFeatureResult forecast:
                update.Forecast = forecast.ToForecastValue();
                break;
            case WeatherResult weather:
                update.Weather = weather.ToWeatherValue();
                break;
            case ObservationResult observation:
                update.Observation = observation.ToObservationValue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.GetType().Name, null);
        }

        return update;
    }
}
