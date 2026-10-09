using System.Text.Json.Nodes;
using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.Mqtt;

public static class DiscoveryPayloadBuilder
{
    public static string Build(
        string location,
        WeatherModel model,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> applicableHorizons,
        IReadOnlyList<int> applicableDayOffsets,
        IReadOnlySet<ParameterDef> supportedParameters,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.DeviceId(location, model);
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var parameter in parameters.Hourly)
        {
            if (!supportedParameters.Contains(parameter))
            {
                continue;
            }

            foreach (var hours in applicableHorizons)
            {
                var horizonTopic = TopicScheme.HorizonTopic(mqtt.BaseTopic, location, model, $"h{hours}");
                var key = $"{parameter.JsonKey}_h{hours}";
                components[key] = BuildParameterComponent(
                    key,
                    TopicScheme.HourlyUniqueId(location, model, parameter, hours),
                    $"{parameter.JsonKey} +{hours}h",
                    parameter,
                    horizonTopic,
                    availabilityTopic,
                    expireAfterSeconds);
            }
        }

        foreach (var parameter in parameters.Daily)
        {
            if (!supportedParameters.Contains(parameter))
            {
                continue;
            }

            foreach (var d in applicableDayOffsets)
            {
                var horizonTopic = TopicScheme.HorizonTopic(mqtt.BaseTopic, location, model, $"d{d}");
                var key = $"{parameter.JsonKey}_d{d}";
                components[key] = BuildParameterComponent(
                    key,
                    TopicScheme.DailyUniqueId(location, model, parameter, d),
                    $"{parameter.JsonKey} d{d}",
                    parameter,
                    horizonTopic,
                    availabilityTopic,
                    expireAfterSeconds);
            }
        }

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} {model.Id}",
                ["mf"] = "njord",
                ["mdl"] = model.Id,
                ["sw"] = version,
            },
            ["o"] = new JsonObject
            {
                ["name"] = "njord",
                ["sw"] = version,
            },
            ["qos"] = 1,
            ["cmps"] = components,
        };

        return payload.ToJsonString();
    }

    public static string BuildConsensus(
        string location,
        ResolvedParameterSet parameters,
        int maxHours,
        int maxDays,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "consensus");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var parameter in parameters.Hourly)
        {
            for (var hours = 0; hours <= maxHours; hours++)
            {
                var horizonTopic = TopicScheme.EnrichmentSubTopic(mqtt.BaseTopic, location, "consensus", $"h{hours}");
                var key = $"{parameter.JsonKey}_h{hours}";
                components[key] = BuildParameterComponent(
                    key,
                    $"{deviceId}_{parameter.JsonKey}_h{hours}",
                    $"{parameter.JsonKey} +{hours}h",
                    parameter,
                    horizonTopic,
                    availabilityTopic,
                    expireAfterSeconds);
            }
        }

        foreach (var parameter in parameters.Daily)
        {
            for (var day = 0; day < maxDays; day++)
            {
                var horizonTopic = TopicScheme.EnrichmentSubTopic(mqtt.BaseTopic, location, "consensus", $"d{day}");
                var key = $"{parameter.JsonKey}_d{day}";
                components[key] = BuildParameterComponent(
                    key,
                    $"{deviceId}_{parameter.JsonKey}_d{day}",
                    $"{parameter.JsonKey} +{day}d",
                    parameter,
                    horizonTopic,
                    availabilityTopic,
                    expireAfterSeconds);
            }
        }

        return BuildDeviceEnvelope(deviceId, location, "consensus", version, components);
    }

    public static string BuildDeviceEnvelope(
        string deviceId,
        string location,
        string typeLabel,
        string version,
        JsonObject components)
    {
        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} {typeLabel}",
                ["mf"] = "njord",
                ["mdl"] = typeLabel,
                ["sw"] = version,
            },
            ["o"] = new JsonObject
            {
                ["name"] = "njord",
                ["sw"] = version,
            },
            ["qos"] = 1,
            ["cmps"] = components,
        };

        return payload.ToJsonString();
    }

    public static JsonObject ToComponent(
        SensorComponentSpec spec,
        string availabilityTopic,
        int expireAfterSeconds)
    {
        var component = new JsonObject
        {
            ["p"] = spec.Platform,
            ["unique_id"] = spec.UniqueId,
            ["name"] = spec.Name,
            ["state_topic"] = spec.StateTopic,
        };

        if (!spec.MeasurementLast)
        {
            AddMeasurement(component, spec);
        }

        if (spec.SuggestedDisplayPrecision is { } precision)
        {
            component["suggested_display_precision"] = precision;
        }

        component["expire_after"] = expireAfterSeconds;
        component["value_template"] = spec.ValueTemplate;

        if (spec.JsonAttributesTopic is not null)
        {
            component["json_attributes_topic"] = spec.JsonAttributesTopic;
        }

        if (spec.JsonAttributesTemplate is not null)
        {
            component["json_attributes_template"] = spec.JsonAttributesTemplate;
        }

        var availability = new JsonArray(new JsonObject { ["topic"] = availabilityTopic });
        if (spec.ExtraAvailability is not null)
        {
            foreach (var (topic, valueTemplate) in spec.ExtraAvailability)
            {
                var entry = new JsonObject { ["topic"] = topic };
                if (valueTemplate is not null)
                {
                    entry["value_template"] = valueTemplate;
                }

                availability.Add(entry);
            }
        }

        component["availability"] = availability;
        component["availability_mode"] = "all";

        if (spec.MeasurementLast)
        {
            AddMeasurement(component, spec);
        }

        return component;
    }

    private static void AddMeasurement(JsonObject component, SensorComponentSpec spec)
    {
        if (spec.Unit is not null)
        {
            component["unit_of_measurement"] = spec.Unit;
        }

        if (spec.DeviceClass is not null)
        {
            component["device_class"] = spec.DeviceClass;
        }
    }

    public static void AddComponents(
        JsonObject components,
        IEnumerable<SensorComponentSpec> specs,
        string availabilityTopic,
        int expireAfterSeconds)
    {
        foreach (var spec in specs)
        {
            components[spec.Key] = ToComponent(spec, availabilityTopic, expireAfterSeconds);
        }
    }

    private static JsonObject BuildParameterComponent(
        string key,
        string uniqueId,
        string name,
        ParameterDef parameter,
        string stateTopic,
        string availabilityTopic,
        int expireAfterSeconds)
    {
        var spec = new SensorComponentSpec(
            key,
            uniqueId,
            name,
            stateTopic,
            $"{{{{ value_json.{parameter.JsonKey} }}}}",
            Unit: parameter.Unit,
            SuggestedDisplayPrecision: 1,
            ExtraAvailability:
            [
                (stateTopic,
                    $"{{% if value_json.{parameter.JsonKey} is not none %}}online{{% else %}}offline{{% endif %}}"),
            ]);

        var component = ToComponent(spec, availabilityTopic, expireAfterSeconds);

        if (parameter.DeviceClass is { } deviceClass)
        {
            component["device_class"] = deviceClass;
        }

        return component;
    }
}
