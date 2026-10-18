## 1. MQTT publisher

- [ ] 1.1 Add `MqttPublishContext` and `PipelineEventMqttMappings` (`ToMqttMessages`, `ToLocationName`) with a mapping spec per event type (topic, payload, retain)
- [ ] 1.2 Reduce `MqttStatePublisher` to context construction, dedup and publish; verify the MQTT golden masters are unchanged

## 2. Open-Meteo client

- [ ] 2.1 Move `MapHourly`, `MapDaily`, `ParseNumberArray`, `ParseMixedArray` into `OpenMeteoSeriesMappings` with specs (values, nulls, time strings, trailing null trimming)
- [ ] 2.2 Move `BuildUri` into `OpenMeteoRequestMappings` with specs (invariant culture, effective days, daily variables) and `UnitMismatch` into `OpenMeteoUnitCheck` with specs
- [ ] 2.3 Reduce `OpenMeteoClient` to HTTP, status handling and outcomes; existing client specs stay green unchanged

## 3. Log level and enforcement

- [ ] 3.1 Replace `ActorCategoryLogger.ToLevel` with a throwing `ToLogLevel()` extension and a spec for every Akka level
- [ ] 3.2 Extend `MappingConventionArchitectureSpec` with the `Map*`/`Parse*Array`/`Build*Uri` rule for actors and services

## 4. Verification

- [ ] 4.1 Update `AGENTS.md` (solution structure: `Njord.OpenMeteo/Mapping`, Mqtt mapping list)
- [ ] 4.2 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
