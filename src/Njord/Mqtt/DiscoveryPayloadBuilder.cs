using System.Text.Json.Nodes;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Domain.Analysis;

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
                continue;

            foreach (var hours in applicableHorizons)
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
            if (!supportedParameters.Contains(parameter))
                continue;

            foreach (var d in applicableDayOffsets)
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
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "consensus");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var parameter in parameters.Hourly)
        {
            foreach (var hours in horizons)
            {
                var horizonTopic = TopicScheme.EnrichmentSubTopic(mqtt.BaseTopic, location, "consensus", $"h{hours}");
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
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "alerts");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var alertType in Enum.GetValues<AlertType>())
        {
            var segment = alertType.ToTopicSegment();
            var topic = TopicScheme.EnrichmentSubTopic(mqtt.BaseTopic, location, "alerts", segment);
            var uniqueId = $"{deviceId}_{segment.Replace('-', '_')}";

            var component = new JsonObject
            {
                ["p"] = "binary_sensor",
                ["unique_id"] = uniqueId,
                ["name"] = segment,
                ["state_topic"] = topic,
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

    public static string BuildHistory(
        string location,
        IReadOnlyList<string> modelIds,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "history");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        foreach (var modelId in modelIds)
        {
            var slug = modelId.Replace('-', '_').ToLowerInvariant();

            foreach (var prefix in new[] { ("mae_7d", "MAE 7d"), ("mae_30d", "MAE 30d"), ("weight", "weight"), ("drift", "drift") })
            {
                var key = $"{prefix.Item1}_{slug}";
                components[key] = new JsonObject
                {
                    ["p"] = "sensor",
                    ["unique_id"] = $"{deviceId}_{key}",
                    ["name"] = $"{prefix.Item2} {modelId}",
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic }),
                    ["availability_mode"] = "all",
                };
            }
        }

        components["seasonal_best"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_seasonal_best",
            ["name"] = "seasonal best model",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.seasonal_best }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["anomaly"] = new JsonObject
        {
            ["p"] = "binary_sensor",
            ["unique_id"] = $"{deviceId}_anomaly",
            ["name"] = "anomaly",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{% if value_json.anomaly == true %}ON{% else %}OFF{% endif %}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["anomaly_deviation"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_anomaly_deviation",
            ["name"] = "anomaly deviation",
            ["unit_of_measurement"] = "σ",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.anomaly_deviation }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["weighted_temperature"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_weighted_temperature",
            ["name"] = "weighted temperature",
            ["unit_of_measurement"] = "°C",
            ["device_class"] = "temperature",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.weighted_temperature }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} history",
                ["mf"] = "njord",
                ["mdl"] = "history",
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

    public static string BuildEnergy(
        string location,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "energy");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);
        var energyTopic = TopicScheme.EnrichmentTopic(mqtt.BaseTopic, location, "energy");

        var components = new JsonObject();

        var numericSensors = new[]
        {
            "heating_demand", "cop_estimate", "shading", "night_cooling",
        };

        foreach (var key in numericSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = key.Replace('_', ' '),
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        components["battery_strategy"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_battery_strategy",
            ["name"] = "battery strategy",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.battery_strategy }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        components["cop_optimal"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_cop_optimal",
            ["name"] = "COP optimal hours",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.cop_optimal | length }}",
            ["json_attributes_topic"] = energyTopic,
            ["json_attributes_template"] = "{{ {'hours': value_json.cop_optimal} | tojson }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} energy",
                ["mf"] = "njord",
                ["mdl"] = "energy",
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

    public static string BuildIndices(
        string location,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "indices");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);
        var indexTopic = TopicScheme.EnrichmentTopic(mqtt.BaseTopic, location, "indices");

        var components = new JsonObject();

        var scoreSensors = new[]
        {
            "laundry", "outdoor", "running", "cycling",
            "bbq", "irrigation", "solar", "ventilation",
        };

        foreach (var key in scoreSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = key.Replace('_', ' '),
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        var degreeDaySensors = new (string Key, string Name)[] { ("hdd", "heating degree days"), ("cdd", "cooling degree days") };
        foreach (var (key, name) in degreeDaySensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["unit_of_measurement"] = "°Cd",
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        var numericSensors = new (string Key, string Name, string Unit)[]
        {
            ("frost_hours", "frost in", "h"),
            ("frost_confidence", "frost confidence", ""),
            ("vpd_kpa", "VPD", "kPa"),
        };

        foreach (var (key, name, unit) in numericSensors)
        {
            var comp = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
            if (!string.IsNullOrEmpty(unit))
                comp["unit_of_measurement"] = unit;
            components[key] = comp;
        }

        components["vpd_category"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{deviceId}_vpd_category",
            ["name"] = "VPD category",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.vpd_category }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} indices",
                ["mf"] = "njord",
                ["mdl"] = "indices",
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

    public static string BuildTrends(
        string location,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "trends");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);
        var trendTopic = TopicScheme.EnrichmentTopic(mqtt.BaseTopic, location, "trends");

        var components = new JsonObject();

        var textSensors = new[]
        {
            ("trend_temperature_dir", "temperature trend"),
            ("trend_wind_speed_dir", "wind trend"),
            ("trend_precipitation_dir", "precipitation trend"),
            ("trend_cloud_cover_dir", "cloud cover trend"),
            ("weather_change", "weather change"),
            ("stability", "stability"),
        };

        foreach (var (key, name) in textSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        var numericSensors = new (string Key, string Name, string Unit)[]
        {
            ("precip_starts", "precip starts in", "h"),
            ("precip_ends", "precip ends in", "h"),
            ("temp_max_in", "temp max in", "h"),
            ("temp_min_in", "temp min in", "h"),
            ("decay_rate", "decay rate", "°C/h"),
            ("reliable_hours", "reliable hours", "h"),
        };

        foreach (var (key, name, unit) in numericSensors)
        {
            components[key] = new JsonObject
            {
                ["p"] = "sensor",
                ["unique_id"] = $"{deviceId}_{key}",
                ["name"] = name,
                ["unit_of_measurement"] = unit,
                ["expire_after"] = expireAfterSeconds,
                ["value_template"] = $"{{{{ value_json.{key} }}}}",
                ["availability"] = new JsonArray(
                    new JsonObject { ["topic"] = availabilityTopic }),
                ["availability_mode"] = "all",
            };
        }

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} trends",
                ["mf"] = "njord",
                ["mdl"] = "trends",
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

    public static string BuildDerived(
        string location,
        IReadOnlyList<int> horizons,
        MqttOptions mqtt,
        TimeSpan pollInterval,
        string version)
    {
        var deviceId = TopicScheme.EnrichmentDeviceId(location, "derived");
        var availabilityTopic = TopicScheme.AvailabilityTopic(mqtt.BaseTopic);
        var expireAfterSeconds = (int)(2 * pollInterval.TotalSeconds);

        var components = new JsonObject();

        var horizonParams = new (string Key, string Name, string? Unit, string? DeviceClass, string Platform)[]
        {
            ("beaufort", "beaufort", null, null, "sensor"),
            ("wind_chill", "wind chill", "°C", "temperature", "sensor"),
            ("dewpoint_comfort", "dew-point comfort", null, null, "sensor"),
            ("wmo_description", "weather", null, null, "sensor"),
        };

        foreach (var hours in horizons)
        {
            var horizonTopic = TopicScheme.EnrichmentSubTopic(mqtt.BaseTopic, location, "derived", $"h{hours}");

            foreach (var (key, name, unit, deviceClass, platform) in horizonParams)
            {
                var uniqueId = $"{deviceId}_{key}_h{hours}";
                var component = new JsonObject
                {
                    ["p"] = platform,
                    ["unique_id"] = uniqueId,
                    ["name"] = $"{name} +{hours}h",
                    ["expire_after"] = expireAfterSeconds,
                    ["value_template"] = $"{{{{ value_json.{key} }}}}",
                    ["availability"] = new JsonArray(
                        new JsonObject { ["topic"] = availabilityTopic },
                        new JsonObject
                        {
                            ["topic"] = horizonTopic,
                            ["value_template"] = $"{{% if value_json.{key} is not none %}}online{{% else %}}offline{{% endif %}}",
                        }),
                    ["availability_mode"] = "all",
                };

                if (unit is not null)
                    component["unit_of_measurement"] = unit;
                if (deviceClass is not null)
                    component["device_class"] = deviceClass;

                components[$"{key}_h{hours}"] = component;
            }
        }

        var metaTopic = TopicScheme.EnrichmentSubTopic(mqtt.BaseTopic, location, "derived", "meta");

        var amplitudeId = $"{deviceId}_diurnal_amplitude";
        components["diurnal_amplitude"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = amplitudeId,
            ["name"] = "diurnal amplitude",
            ["unit_of_measurement"] = "°C",
            ["device_class"] = "temperature",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.diurnal_amplitude }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        var sunshineId = $"{deviceId}_sunshine_pct";
        components["sunshine_pct"] = new JsonObject
        {
            ["p"] = "sensor",
            ["unique_id"] = sunshineId,
            ["name"] = "sunshine",
            ["unit_of_measurement"] = "%",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{{ value_json.sunshine_pct }}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        var inversionId = $"{deviceId}_inversion";
        components["inversion"] = new JsonObject
        {
            ["p"] = "binary_sensor",
            ["unique_id"] = inversionId,
            ["name"] = "inversion",
            ["expire_after"] = expireAfterSeconds,
            ["value_template"] = "{% if value_json.inversion == true %}ON{% else %}OFF{% endif %}",
            ["availability"] = new JsonArray(
                new JsonObject { ["topic"] = availabilityTopic }),
            ["availability_mode"] = "all",
        };

        var payload = new JsonObject
        {
            ["dev"] = new JsonObject
            {
                ["ids"] = new JsonArray(deviceId),
                ["name"] = $"njord {location} derived",
                ["mf"] = "njord",
                ["mdl"] = "derived",
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

    internal static JsonObject BuildComponent(
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
            ["state_topic"] = stateTopic,
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
