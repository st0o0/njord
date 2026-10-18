## Context

Remaining in-actor mapping code, found by scanning the production projects: `MqttStatePublisher` (`MapToMqttMessages`, `MapPerModel`, also an inline location switch), `OpenMeteoClient` (`MapHourly`, `MapDaily`, `ParseNumberArray`, `ParseMixedArray`, `BuildUri`, `UnitMismatch`) and `ActorCategoryLogger.ToLevel`. Not in scope: `ConfigManager.Apply*` (state transitions, not mappings), `OpenMeteoGateway.ResolveModel` (a lookup), and the persistence mappers in `PollWorkerState`/`BudgetTrackerState` (own change `persistence-mapping-and-enums`).

## Decisions

1. **MQTT: context record instead of actor fields.** `MqttPublishContext(string BaseTopic, ResolvedParameterSet Parameters, IReadOnlyList<int> Horizons, int ForecastDays, DateTimeOffset Now)` carries what the actor currently reads from its fields; the extension returns `IEnumerable<MqttMessage>` and the location comes from `ToLocationName()` (the actor builds `OutgoingMessage` itself). The `StatePayloadBuilder` calls stay as they are.
2. **Open-Meteo: mapping receives the parameter set.** The series mappers use the primary-constructor field `parameters`; as static extensions they take `ResolvedParameterSet` explicitly. JSON array parsing is internal to `OpenMeteoSeriesMappings`. URI construction takes the location, model, effective days and the precomputed variable lists; the client keeps caching the variable strings.
3. **Unit check as a small static class**, because it returns a diagnostic string, not a mapped type. It keeps using `ParameterDef.DeviceClass` for now; `parameter-registry-redesign` replaces that input.
4. **Throwing default for log levels.** Akka has four levels; the current `_ => Error` hides undefined values. Enum convention applies, so the default throws.
5. **Architecture rule by name.** Actor detection via `ActorBase` assignability, services via the type names registered in the setup containers is too fragile; the rule scans all production classes that are not `*Mappings`/`*Check` for the three name patterns, with an explicit allow-list for legitimate leftovers (none expected).

## Risks / Trade-offs

- Moving `BuildUri` out of the client changes where float formatting (`CultureInfo.InvariantCulture`) lives; the existing client specs that assert the requested URI must stay green unchanged.
- Passing a context record allocates per event; negligible next to payload serialization.

## Migration Plan

Pure refactor in three steps (MQTT, Open-Meteo, log level), each ending green with unchanged golden masters and client specs.
