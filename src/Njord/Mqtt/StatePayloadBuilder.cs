using System.Text.Json.Nodes;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Mqtt;

public static class StatePayloadBuilder
{
    public static Dictionary<string, string> BuildPerHorizon(
        ModelForecast forecast,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays,
        DateTimeOffset anchorTime)
        => HorizonProjection.BuildPerHorizon(forecast, parameters, horizons, forecastDays, anchorTime);

    public static IReadOnlyList<MqttMessage> FromConsensus(
        ConsensusResult result, string baseTopic, string location)
    {
        var messages = new List<MqttMessage>();
        var horizonPayloads = new Dictionary<string, JsonObject>();

        foreach (var pc in result.Parameters)
        {
            foreach (var (horizon, hc) in pc.ByHorizon)
            {
                if (!horizonPayloads.TryGetValue(horizon, out var payload))
                {
                    payload = new JsonObject();
                    horizonPayloads[horizon] = payload;
                }

                payload[pc.Parameter.JsonKey] = hc.Median.HasValue
                    ? JsonValue.Create(Math.Round(hc.Median.Value, 2))
                    : null;
                payload[$"{pc.Parameter.JsonKey}_models"] = hc.AvailableModels.Count;
            }
        }

        foreach (var (horizon, payload) in horizonPayloads)
        {
            var firstParam = result.Parameters.FirstOrDefault()?.ByHorizon.GetValueOrDefault(horizon);
            if (firstParam is not null)
            {
                payload["_spread"] = firstParam.Spread.HasValue
                    ? JsonValue.Create(Math.Round(firstParam.Spread.Value, 2))
                    : null;
                payload["_agreement"] = firstParam.Agreement.HasValue
                    ? JsonValue.Create(Math.Round(firstParam.Agreement.Value, 3))
                    : null;
            }

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, location, "consensus", horizon);
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }

        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromAlerts(AlertResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>(result.Alerts.Count);
        foreach (var alert in result.Alerts)
        {
            var payload = new JsonObject
            {
                ["severity"] = alert.Severity.ToString().ToLowerInvariant(),
                ["confidence"] = alert.Confidence,
            };

            foreach (var (attrKey, attrValue) in alert.Attributes)
            {
                payload[attrKey] = attrValue switch
                {
                    double d => JsonValue.Create(d),
                    int i => JsonValue.Create(i),
                    string s => JsonValue.Create(s),
                    null => null,
                    _ => JsonValue.Create(attrValue.ToString()),
                };
            }

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location, "alerts", alert.Type.ToTopicSegment());
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }
        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromDerived(DerivedResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>();

        foreach (var (horizon, hd) in result.ByHorizon)
        {
            var payload = new JsonObject
            {
                ["beaufort"] = hd.Beaufort.HasValue ? JsonValue.Create(hd.Beaufort.Value) : null,
                ["wind_chill"] = hd.WindChill.HasValue ? JsonValue.Create(Math.Round(hd.WindChill.Value, 1)) : null,
                ["dewpoint_comfort"] = hd.DewPointComfort is { } dc ? JsonValue.Create(dc) : null,
                ["wmo_description"] = hd.WmoDescription is { } wd ? JsonValue.Create(wd) : null,
            };

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location, "derived", horizon);
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }

        var meta = new JsonObject
        {
            ["diurnal_amplitude"] = result.Scalars.DiurnalAmplitude.HasValue
                ? JsonValue.Create(Math.Round(result.Scalars.DiurnalAmplitude.Value, 1))
                : null,
            ["sunshine_pct"] = result.Scalars.SunshinePct.HasValue
                ? JsonValue.Create(Math.Round(result.Scalars.SunshinePct.Value, 1))
                : null,
            ["inversion"] = result.Scalars.Inversion.HasValue
                ? JsonValue.Create(result.Scalars.Inversion.Value)
                : null,
        };

        var metaTopic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location, "derived", "meta");
        messages.Add(new MqttMessage(metaTopic, meta.ToJsonString(), true));

        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromTrends(TrendResult result, string baseTopic)
    {
        var payload = new JsonObject();

        foreach (var (apiName, trend) in result.ParameterTrends)
        {
            var key = $"trend_{apiName.Replace("_2m", "").Replace("_10m", "")}";
            payload[$"{key}_dir"] = trend?.Direction;
            payload[$"{key}_delta"] = trend is { } t ? JsonValue.Create(t.Delta) : null;
        }

        payload["weather_change"] = result.WeatherChange?.Description;

        payload["precip_starts"] = result.PrecipTiming.StartsInHours.HasValue
            ? JsonValue.Create(result.PrecipTiming.StartsInHours.Value) : null;
        payload["precip_ends"] = result.PrecipTiming.EndsInHours.HasValue
            ? JsonValue.Create(result.PrecipTiming.EndsInHours.Value) : null;

        payload["temp_max_in"] = result.ExtremaTiming.MaxInHours.HasValue
            ? JsonValue.Create(result.ExtremaTiming.MaxInHours.Value) : null;
        payload["temp_min_in"] = result.ExtremaTiming.MinInHours.HasValue
            ? JsonValue.Create(result.ExtremaTiming.MinInHours.Value) : null;

        payload["stability"] = result.Stability?.Label;
        payload["stability_ratio"] = result.Stability is { } s ? JsonValue.Create(s.Ratio) : null;

        payload["decay_rate"] = result.Decay is { } d ? JsonValue.Create(d.DecayRate) : null;
        payload["reliable_hours"] = result.Decay?.ReliableHours is { } rh ? JsonValue.Create(rh) : null;

        var topic = TopicScheme.EnrichmentTopic(baseTopic, result.Location, "trends");
        return [new MqttMessage(topic, payload.ToJsonString(), true)];
    }

    public static IReadOnlyList<MqttMessage> FromIndices(IndexResult result, string baseTopic)
    {
        var payload = new JsonObject
        {
            ["laundry"] = result.Laundry,
            ["outdoor"] = result.Outdoor,
            ["running"] = result.Running,
            ["cycling"] = result.Cycling,
            ["bbq"] = result.Bbq,
            ["irrigation"] = result.Irrigation,
            ["hdd"] = JsonValue.Create(Math.Round(result.Hdd, 1)),
            ["cdd"] = JsonValue.Create(Math.Round(result.Cdd, 1)),
            ["solar"] = result.Solar,
            ["ventilation"] = result.Ventilation,
            ["frost_hours"] = result.FrostProtection?.HoursUntilFrost is { } fh ? JsonValue.Create(fh) : null,
            ["frost_confidence"] = result.FrostProtection?.Confidence is { } fc ? JsonValue.Create(fc) : null,
            ["vpd_category"] = result.Vpd?.Category,
            ["vpd_kpa"] = result.Vpd?.Vpd is { } v ? JsonValue.Create(v) : null,
        };

        var topic = TopicScheme.EnrichmentTopic(baseTopic, result.Location, "indices");
        return [new MqttMessage(topic, payload.ToJsonString(), true)];
    }

    public static IReadOnlyList<MqttMessage> FromEnergy(EnergyResult result, string baseTopic)
    {
        var copArray = new JsonArray();
        foreach (var (h, c) in result.CopOptimal)
        {
            copArray.Add(new JsonObject { ["hour"] = h, ["cop"] = Math.Round(c, 2) });
        }

        var payload = new JsonObject
        {
            ["heating_demand"] = result.HeatingDemand,
            ["cop_estimate"] = result.CopEstimate.HasValue ? JsonValue.Create(result.CopEstimate.Value) : null,
            ["cop_optimal"] = copArray,
            ["shading"] = result.Shading,
            ["battery_strategy"] = result.BatteryStrategy,
            ["night_cooling"] = result.NightCooling,
        };

        var topic = TopicScheme.EnrichmentTopic(baseTopic, result.Location, "energy");
        return [new MqttMessage(topic, payload.ToJsonString(), true)];
    }

    public static IReadOnlyList<MqttMessage> FromHistory(HistoryResult result, string baseTopic)
    {
        var payload = new JsonObject();

        foreach (var (model, mae) in result.Mae7d)
            payload[$"mae_7d_{model.Id}"] = mae.HasValue ? JsonValue.Create(mae.Value) : null;

        foreach (var (model, mae) in result.Mae30d)
            payload[$"mae_30d_{model.Id}"] = mae.HasValue ? JsonValue.Create(mae.Value) : null;

        foreach (var (model, w) in result.Weights)
            payload[$"weight_{model.Id}"] = JsonValue.Create(w);

        foreach (var (model, d) in result.Drift)
            payload[$"drift_{model.Id}"] = d.HasValue ? JsonValue.Create(d.Value) : null;

        payload["seasonal_best"] = result.SeasonalBest?.Id;
        payload["anomaly"] = result.Anomaly?.IsAnomaly;
        payload["anomaly_deviation"] = result.Anomaly?.DeviationSigma is { } dev ? JsonValue.Create(dev) : null;
        payload["weighted_temperature"] = result.WeightedTemperature.HasValue ? JsonValue.Create(result.WeightedTemperature.Value) : null;

        var topic = TopicScheme.EnrichmentTopic(baseTopic, result.Location, "history");
        return [new MqttMessage(topic, payload.ToJsonString(), true)];
    }
}
