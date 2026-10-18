## Why

`consistent-mappings` moved the gRPC, config and options mappings into static extension classes, but mapping code still sits inside actors and services: `MqttStatePublisher` turns pipeline events into MQTT messages, `OpenMeteoClient` maps API series into domain series (a 110-line private mapping plus JSON array parsing and URI building), and `ActorCategoryLogger` converts log levels. These are untestable without the host class, mix I/O and mapping, and break the "mapping code never lives in actors or services" rule the convention now states.

## What Changes

- `MqttStatePublisher.MapToMqttMessages` and `MapPerModel` become `PipelineEvent.ToMqttMessages(MqttPublishContext)` and `PipelineEvent.ToLocationName()` in `Njord.Mqtt/Mapping/PipelineEventMqttMappings`; the actor only passes its settings in a small `MqttPublishContext` record, dedups and publishes.
- `OpenMeteoClient.MapHourly`, `MapDaily`, `ParseNumberArray`, `ParseMixedArray` and `BuildUri` move into `Njord.OpenMeteo/Mapping` (`OpenMeteoSeriesMappings`: `ToForecastSeries(ResolvedParameterSet)` / `ToDailySeries(...)`, `OpenMeteoRequestMappings`: `ToRequestUri(...)`); the unit check becomes a static `OpenMeteoUnitCheck`. The client keeps HTTP, retries and outcome handling.
- `ActorCategoryLogger.ToLevel` becomes a `ToLogLevel()` switch extension with a throwing default.
- `MappingConventionArchitectureSpec` also fails when an actor or service class declares a method named `Map*`, `Parse*Array` or `Build*Uri`.
- Behaviour, wire format, URIs, payloads and metrics are unchanged; existing OpenMeteo client specs and MQTT golden masters are the proof.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `mapping-conventions`: the "no mapping code in actors or services" requirement gets concrete scenarios for the MQTT publisher and the Open-Meteo client, and the architecture rule is widened.

## Impact

- `Njord.Mqtt/Actors/MqttStatePublisher.cs`, `Njord.Mqtt/Mapping/`, `Njord.OpenMeteo/OpenMeteoClient.cs`, new `Njord.OpenMeteo/Mapping/`, `Njord/Configuration/ActorCategoryLogger.cs`, `Njord.Architecture.Tests`, matching specs.
- The Open-Meteo unit check currently reads `ParameterDef.DeviceClass`; the `parameter-registry-redesign` change replaces that with an explicit expected-unit rule, so this change keeps the check's behaviour as it is and the redesign adapts the new `OpenMeteoUnitCheck`.
- No breaking change; commit type `refactor:`. Applies after `config-defaults-in-appsettings`.
