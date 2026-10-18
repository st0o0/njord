## Why

`ParameterRegistry` (304 lines in `Njord.Weather`) is hard to maintain and mixes concerns. Every parameter appears twice as a string (`BuildAll()` and again in the static constructor via `Require("temperature_2m")`), so a typo only fails at runtime. `ParameterDef` carries ingest data (API name, unit), configuration data (group), a Home Assistant concept (`DeviceClass`, used only by the Open-Meteo unit check) and an egress concept (`JsonKey`, used only by MQTT). Configuration parsing (`Resolve`, `Enum.TryParse`, operator error messages, `ParameterResolutionException`) lives in the domain registry and is the single entry in the enum-convention allow-list. Three API names exist as hourly and daily variants and are told apart by a custom `Equals` and `RequireDaily`.

## What Changes

- `ParameterRegistry` declares every parameter exactly once as a typed `static readonly ParameterDef` field (`Temperature2m`, `DailyWeatherCode`, ...); the field initializers register into the catalog list. No static constructor, no `Require*`, no second string list.
- `ParameterDef` keeps identity only: `ApiName`, `Unit`, `Group`, `Granularity`, `ValueType`. `DeviceClass` is removed; `JsonKey` leaves the domain.
- `ToMqttKey()` (switch-free table lookup is not allowed by the enum rule, so a dictionary keyed by `ParameterDef` inside `Njord.Mqtt`) replaces `JsonKey` for consensus and horizon payload keys; the keys stay byte-identical.
- The Open-Meteo unit check validates units by the `Unit` itself (`°C`, `m/s`, `hPa`) instead of `DeviceClass`; behaviour stays the same.
- Resolution moves to `Njord.Core/Configuration` as `ParameterSelection`: groups are parsed by an exhaustive switch (no `Enum.TryParse`), the operator error messages and `ParameterResolutionException` move with it, and `ResolvedParameterSet` keeps its API in Weather.
- The `ParameterRegistry` entry leaves the enum-convention allow-list; `GetByApiName` stays for deserialization only.

## Capabilities

### New Capabilities
- `parameter-selection`: configuration-time resolution of groups, extras and excludes into a `ResolvedParameterSet`.

### Modified Capabilities
- `parameter-registry`: catalog entries carry identity only; resolution is no longer part of the registry.
- `parameter-typed-access`: typed properties are compile-time declarations without a throwing static constructor lookup.

## Impact

- `Njord.Weather/{ParameterRegistry,ParameterDef}.cs`, `Njord.Core/Configuration/{ParameterOptionsExtensions,NjordOptionsValidator,Polling/PollingOptionsValidator}.cs`, `Njord.Mqtt/{Mapping/ForecastPayloadMappings,Mapping/ConsensusPayloadMappings,Topics/TopicScheme}.cs`, `Njord.OpenMeteo/OpenMeteoClient.cs` (unit check), `Njord.Architecture.Tests` (allow-list), specs for each.
- `ParameterRegistry.X` call sites (426 in 70 files) stay unchanged.
- No wire change: gRPC and MQTT golden masters and the Open-Meteo request URIs stay identical.
- Not breaking; commit type `refactor:`. Applies after `extract-actor-mappings`, which creates the `OpenMeteoUnitCheck` this change adapts.
