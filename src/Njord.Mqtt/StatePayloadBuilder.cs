using System.Text.Json.Nodes;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Querying;
using Njord.Messages.Pipeline;
using Njord.Weather;

namespace Njord.Mqtt;

public static class StatePayloadBuilder
{
    public static Dictionary<Horizon, string> BuildPerHorizon(
        Forecast forecast,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays,
        DateTimeOffset anchorTime)
        => HorizonProjection.BuildPerHorizon(forecast, parameters, horizons, forecastDays, anchorTime);

    public static IReadOnlyList<MqttMessage> FromConsensus(
        ConsensusResult consensus, string baseTopic, string location)
    {
        var messages = new List<MqttMessage>();

        EmitConsensusMessages(consensus.Parameters, baseTopic, location, messages);
        EmitConsensusMessages(consensus.DailyParameters, baseTopic, location, messages);

        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromConsensus(
        ConsensusSnapshot snapshot, string baseTopic)
        => FromConsensus(
            new ConsensusResult(snapshot.Hourly.Parameters, snapshot.Daily.Parameters, snapshot.ComputedAt),
            baseTopic, snapshot.Location.Name);

    private static void EmitConsensusMessages(
        IReadOnlyList<ParameterConsensus> parameters,
        string baseTopic, string location,
        List<MqttMessage> messages)
    {
        var horizonPayloads = new Dictionary<Horizon, JsonObject>();

        foreach (var pc in parameters)
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
            var firstParam = parameters.FirstOrDefault().At(horizon);
            if (firstParam is not null)
            {
                payload["_spread"] = firstParam.Spread.HasValue
                    ? JsonValue.Create(Math.Round(firstParam.Spread.Value, 2))
                    : null;
                payload["_agreement"] = firstParam.Agreement.HasValue
                    ? JsonValue.Create(Math.Round(firstParam.Agreement.Value, 3))
                    : null;
            }

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, location, EnrichmentKind.Consensus.ToWireName(), horizon.ToWireKey());
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }
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
                ["trigger_value"] = JsonValue.Create(alert.TriggerValue),
                ["threshold"] = JsonValue.Create(alert.Threshold),
            };

            if (alert.PeakValue is { } pv)
            {
                payload["peak_value"] = JsonValue.Create(pv);
            }

            if (alert.HoursUntil is { } hu)
            {
                payload["hours_until"] = JsonValue.Create(hu);
            }

            if (alert.DurationHours is { } dh)
            {
                payload["duration_hours"] = JsonValue.Create(dh);
            }

            AddAlertDetails(payload, alert.Details);

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Alerts.ToWireName(), alert.Type.ToTopicSegment());
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }
        return messages;
    }

    private static void AddAlertDetails(JsonObject payload, AlertDetails details)
    {
        switch (details)
        {
            case FrostDetails d:
                payload["expected_low"] = d.ExpectedLow;
                payload["earliest_frost"] = d.EarliestFrost is { } earliest
                    ? JsonValue.Create($"+{earliest.Offset}h")
                    : null;
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case HeatDetails d:
                payload["expected_max"] = d.ExpectedMax;
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case StormDetails d:
                payload["expected_max_gust"] = d.ExpectedMaxGust;
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case HeavyRainDetails d:
                payload["hourly_exceed_models"] = d.HourlyExceedModels;
                payload["daily_exceed_models"] = d.DailyExceedModels;
                break;
            case UvDetails d:
                payload["uv_level"] = d.Level.ToWireName();
                payload["uv_index"] = d.UvIndex;
                break;
            case FogDetails d:
                payload["fog_hours"] = d.FogHours;
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case SnowDetails d:
                payload["expected_accumulation"] = d.ExpectedAccumulation;
                payload["freezing_level"] = d.FreezingLevel is { } level ? JsonValue.Create(level) : null;
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case PressureDropDetails d:
                payload["max_drop"] = d.MaxDrop;
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case ThunderstormDetails d:
                payload["models_agreeing"] = d.ModelsAgreeing;
                break;
            case IceDetails d:
                payload["expected_low"] = d.ExpectedLow;
                payload["rain_hours"] = d.RainHours;
                payload["soil_frozen"] = d.SoilFrozen ? "True" : "False";
                break;
            case WindChillDetails d:
                payload["felt_temperature"] = d.FeltTemperature;
                payload["wind_factor"] = d.WindFactor;
                payload["exposure_risk"] = d.ExposureRisk?.ToWireName();
                break;
            case VisibilityDetails d:
                payload["min_visibility"] = d.MinVisibility;
                payload["hours_below_1000m"] = d.HoursBelow1000m;
                break;
            case TropicalNightDetails d:
                payload["night_minimum"] = d.NightMinimum;
                payload["hours_above_20"] = d.HoursAbove20;
                break;
            case HumidityDetails d:
                payload["max_dewpoint"] = d.MaxDewpoint;
                payload["comfort_level"] = d.ComfortLevel?.ToWireName();
                break;
        }
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
                ["dewpoint_comfort"] = hd.DewPointComfort is { } dc ? JsonValue.Create(dc.ToWireName()) : null,
                ["wmo_description"] = hd.Condition is { } wd ? JsonValue.Create(wd.ToDescription()) : null,
            };

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Derived.ToWireName(), horizon.ToWireKey());
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

        var metaTopic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Derived.ToWireName(), "meta");
        messages.Add(new MqttMessage(metaTopic, meta.ToJsonString(), true));

        return messages;
    }

    public static IReadOnlyList<MqttMessage> FromTrends(TrendResult result, string baseTopic)
    {
        var payload = new JsonObject();

        foreach (var (apiName, trend) in result.ParameterTrends)
        {
            var key = $"trend_{apiName.Replace("_2m", "").Replace("_10m", "")}";
            payload[$"{key}_dir"] = trend?.Direction.ToWireName();
            payload[$"{key}_delta"] = trend is { } t ? JsonValue.Create(t.Delta) : null;
        }

        payload["weather_change"] = result.WeatherChange?.ToDescription();

        payload["precip_starts"] = result.PrecipTiming.StartsInHours.HasValue
            ? JsonValue.Create(result.PrecipTiming.StartsInHours.Value) : null;
        payload["precip_ends"] = result.PrecipTiming.EndsInHours.HasValue
            ? JsonValue.Create(result.PrecipTiming.EndsInHours.Value) : null;

        payload["temp_max_in"] = result.ExtremaTiming.MaxInHours.HasValue
            ? JsonValue.Create(result.ExtremaTiming.MaxInHours.Value) : null;
        payload["temp_min_in"] = result.ExtremaTiming.MinInHours.HasValue
            ? JsonValue.Create(result.ExtremaTiming.MinInHours.Value) : null;

        payload["stability"] = result.Stability?.Stability.ToWireName();
        payload["stability_ratio"] = result.Stability is { } s ? JsonValue.Create(s.Ratio) : null;

        payload["decay_rate"] = result.Decay is { } d ? JsonValue.Create(d.DecayRate) : null;
        payload["reliable_hours"] = result.Decay?.ReliableHours is { } rh ? JsonValue.Create(rh) : null;

        var topic = TopicScheme.EnrichmentTopic(baseTopic, result.Location.Name, EnrichmentKind.Trends.ToWireName());
        return [new MqttMessage(topic, payload.ToJsonString(), true)];
    }

    public static IReadOnlyList<MqttMessage> FromIndices(IndexResult result, string baseTopic)
    {
        var messages = new List<MqttMessage>();

        foreach (var day in result.Days ?? [])
        {
            var payload = new JsonObject
            {
                ["laundry"] = day.Laundry,
                ["outdoor"] = day.Outdoor,
                ["running"] = day.Running,
                ["cycling"] = day.Cycling,
                ["bbq"] = day.Bbq,
                ["irrigation"] = day.Irrigation,
                ["solar"] = day.Solar,
                ["night_ventilation"] = day.NightVentilation,
                ["hours_included"] = day.HoursIncluded,
            };

            if (day.DayOffset == 0)
            {
                payload["frost_hours"] = result.FrostProtection?.HoursUntilFrost is { } fh ? JsonValue.Create(fh) : null;
                payload["frost_confidence"] = result.FrostProtection?.Confidence is { } fc ? JsonValue.Create(fc) : null;
                payload["vpd_category"] = result.Vpd?.Category;
                payload["vpd_kpa"] = result.Vpd?.Vpd is { } v ? JsonValue.Create(v) : null;
            }

            AddEnvelope(payload, "laundry", day.LaundryEnvelope);
            AddEnvelope(payload, "outdoor", day.OutdoorEnvelope);
            AddEnvelope(payload, "running", day.RunningEnvelope);
            AddEnvelope(payload, "cycling", day.CyclingEnvelope);
            AddEnvelope(payload, "bbq", day.BbqEnvelope);
            AddEnvelope(payload, "irrigation", day.IrrigationEnvelope);
            AddEnvelope(payload, "solar", day.SolarEnvelope);
            AddEnvelope(payload, "night_ventilation", day.NightVentilationEnvelope);

            var topic = TopicScheme.EnrichmentSubTopic(baseTopic, result.Location.Name, EnrichmentKind.Indices.ToWireName(), Horizon.Days(day.DayOffset).ToWireKey());
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }

        return messages;
    }

    private static void AddEnvelope(JsonObject payload, string key, ScoreEnvelope? envelope)
    {
        if (envelope is null)
        {
            return;
        }

        payload[$"{key}_min"] = envelope.Min;
        payload[$"{key}_max"] = envelope.Max;
        payload[$"{key}_confidence"] = JsonValue.Create(envelope.Confidence);
    }
}
