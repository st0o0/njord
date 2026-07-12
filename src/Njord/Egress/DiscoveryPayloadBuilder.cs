using System.Text.Json.Nodes;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Egress;

/// <summary>
/// Builds one device-based discovery payload per (location, model): device and
/// origin blocks plus one sensor component per (parameter, horizon). Shape
/// verified against the HA MQTT docs (2026-07-12).
/// </summary>
public static class DiscoveryPayloadBuilder
{
    public static string Build(
        string location,
        WeatherModel model,
        IReadOnlyList<int> horizons,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.DeviceId(location, model);
        var stateTopic = TopicScheme.StateTopic(mqtt.BaseTopic, location, model);
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        // Twice the poll interval: one missed cycle is tolerated, a second one
        // ages the values out to unavailable.
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();
        foreach (var parameter in Enum.GetValues<WeatherParameter>())
        {
            foreach (var hours in horizons)
            {
                var key = parameter.JsonKey();
                var component = new JsonObject
                {
                    ["p"] = "sensor",
                    ["unique_id"] = TopicScheme.UniqueId(location, model, parameter, hours),
                    ["name"] = $"{key} +{hours}h",
                    ["unit_of_measurement"] = parameter.Unit(),
                    ["suggested_display_precision"] = 1,
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.h{hours}.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic },
                        new JsonObject
                        {
                            ["topic"] = stateTopic,
                            ["value_template"] =
                                $"{{% if value_json.h{hours}.{key} is not none %}}online{{% else %}}offline{{% endif %}}",
                        }),
                    ["availability_mode"] = "all",
                };
                if (parameter.DeviceClass() is { } deviceClass)
                {
                    component["device_class"] = deviceClass;
                }

                components[$"{key}_h{hours}"] = component;
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
            ["state_topic"] = stateTopic,
            ["qos"] = 1,
            ["cmps"] = components,
        };

        return payload.ToJsonString();
    }
}
