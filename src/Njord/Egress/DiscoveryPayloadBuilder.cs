using System.Text.Json.Nodes;
using Njord.Configuration;
using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Egress;

public static class DiscoveryPayloadBuilder
{
    public static string Build(
        string location,
        WeatherModel model,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        int forecastDays,
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
            foreach (var hours in horizons)
            {
                var horizonTopic = TopicScheme.HorizonTopic(mqtt.BaseTopic, location, model, $"h{hours}");
                var component = BuildComponent(
                    TopicScheme.HourlyUniqueId(location, model, parameter, hours),
                    $"{parameter.JsonKey} +{hours}h",
                    parameter,
                    $"{{{{ value_json.{parameter.JsonKey} }}}}",
                    horizonTopic,
                    $"{{% if value_json.{parameter.JsonKey} is not none %}}online{{% else %}}offline{{% endif %}}",
                    availabilityTopic,
                    expireAfterSeconds);

                components[$"{parameter.JsonKey}_h{hours}"] = component;
            }
        }

        foreach (var parameter in parameters.Daily)
        {
            for (var d = 0; d < forecastDays; d++)
            {
                var horizonTopic = TopicScheme.HorizonTopic(mqtt.BaseTopic, location, model, $"d{d}");
                var component = BuildComponent(
                    TopicScheme.DailyUniqueId(location, model, parameter, d),
                    $"{parameter.JsonKey} d{d}",
                    parameter,
                    $"{{{{ value_json.{parameter.JsonKey} }}}}",
                    horizonTopic,
                    $"{{% if value_json.{parameter.JsonKey} is not none %}}online{{% else %}}offline{{% endif %}}",
                    availabilityTopic,
                    expireAfterSeconds);

                components[$"{parameter.JsonKey}_d{d}"] = component;
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
        IReadOnlyList<int> horizons,
        int forecastDays,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.ConsensusDeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var parameter in parameters.Hourly)
        {
            foreach (var hours in horizons)
            {
                var horizonTopic = TopicScheme.ConsensusHorizonTopic(mqtt.BaseTopic, location, $"h{hours}");
                var uniqueId = $"{deviceId}_{parameter.JsonKey}_h{hours}";
                var component = BuildComponent(
                    uniqueId,
                    $"{parameter.JsonKey} +{hours}h",
                    parameter,
                    $"{{{{ value_json.{parameter.JsonKey} }}}}",
                    horizonTopic,
                    $"{{% if value_json.{parameter.JsonKey} is not none %}}online{{% else %}}offline{{% endif %}}",
                    availabilityTopic,
                    expireAfterSeconds);

                components[$"{parameter.JsonKey}_h{hours}"] = component;
            }
        }

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} consensus",
                ["mf"] = "njord",
                ["mdl"] = "consensus",
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

    public static string BuildAlerts(
        string location,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.AlertDeviceId(location);
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var alertType in Enum.GetValues<AlertType>())
        {
            var segment = alertType.ToTopicSegment();
            var topic = TopicScheme.AlertTopic(mqtt.BaseTopic, location, segment);
            var uniqueId = $"{deviceId}_{segment.Replace('-', '_')}";

            var component = new JsonObject
            {
                ["p"] = "binary_sensor",
                ["unique_id"] = uniqueId,
                ["name"] = segment,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = "{% if value_json.severity != 'none' %}ON{% else %}OFF{% endif %}",
                ["json_attributes_topic"] = topic,
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };

            components[segment.Replace('-', '_')] = component;
        }

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} alerts",
                ["mf"] = "njord",
                ["mdl"] = "alerts",
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

    private static JsonObject BuildComponent(
        string uniqueId,
        string name,
        ParameterDef parameter,
        string valueTemplate,
        string stateTopic,
        string availabilityValueTemplate,
        string availabilityTopic,
        int expireAfterSeconds)
    {
        var component = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = uniqueId,
            ["name"] = name,
            ["unit_of_measurement"] = parameter.Unit,
            ["suggested_display_precision"] = 1,
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = valueTemplate,
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic },
                new JsonObject
                {
                    ["topic"] = stateTopic,
                    ["value_template"] = availabilityValueTemplate,
                }),
            ["availability_mode"] = "all",
        };

        if (parameter.DeviceClass is { } deviceClass)
        {
            component["device_class"] = deviceClass;
        }

        return component;
    }
}
