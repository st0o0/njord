## 0. MQTT boundary (pure refactor, no behaviour change; suite stays green)

- [x] 0.1 `src/Njord.Architecture.Tests`: failing boundary spec first (types with `Mqtt` in name or namespace only in `Njord.Mqtt` plus the `Njord.Core` allow-list: `MqttOptions`, `MqttOptionsValidator`, `ConfigReloadPolicy` entry, `IMqttConnectionActor`, `IMqttStatePublisher`, `MqttHealthSnapshot`); adjust `LayerReferenceSpec`/zone specs that mention `Njord.Messages/Mqtt`
- [x] 0.2 Move `MqttConnected`, `SubscribeInbound`, `MqttInboundMessage` from `src/Njord.Messages/Mqtt/` to `src/Njord.Mqtt/Actors/` as internal actor messages; update `MqttConnection`, `Telemetry`, `Njord.Mqtt.Tests` usings; delete the empty folder
- [x] 0.3 Move `src/Njord/Health/MqttConnectionHealthCheck.cs` to `src/Njord.Mqtt/Health/`; register it in `MqttSetupContainer`; remove the MQTT branch from `src/Njord/Configuration/CoreSetupContainer.cs`; add the health-check abstractions reference to `Njord.Mqtt` with `dotnet add package` (or the framework reference the other adapter projects use); health check spec stays green
- [x] 0.4 Restructure `src/Njord.Mqtt` into `Actors/`, `Topics/`, `Health/`, `Transport/`, `Configuration/` (and create `Payloads/`, `Mapping/`, `Serialization/` in the next sections); namespaces follow folders; update all usings including `Njord.Tests` and `Njord.Pipeline.Tests`
- [x] 0.5 Enum conversion convention: specs first pinning `AlertType.ToTopicSegment()` and `EnrichmentKind.ToWireName()` for every member (values unchanged); replace the `ToString()` based arms with explicit switch expressions (throwing default); add the source-scan spec in `Njord.Architecture.Tests` (no `ToString().ToLower*`/`Enum.TryParse`/`Enum.Parse` in production code except the `ParameterRegistry` allow-list); extend the spec to cover the `typed-wire-enums` mappers
- [x] 0.6 Update `AGENTS.md` solution structure (no `Mqtt/` under `Njord.Messages`; the `Njord.Mqtt` folder layout; the allow-list and why), add the enum conversion convention under C# conventions, and run the full test loop

## 1. Foundation for typed payloads (no wire change)

- [ ] 1.1 Spike in `src/Njord.Mqtt`: prove the source-generated `MqttPayloadJsonContext` handles (a) a base record with `[JsonDerivedType]` derived members written after base members, (b) a custom converter for registry-driven ordered entries, (c) enum members written via `ToWireName()`; decide fallback per design decision 4 and record the result in design.md
- [ ] 1.2 `Njord.Mqtt.csproj`: `InternalsVisibleTo` for `Njord.Mqtt.Tests` (already present, verify); shared serializer options in `Serialization/`
- [ ] 1.3 `Njord.Architecture.Tests`: sealed records, no `JsonObject`/`JsonNode` in `Njord.Mqtt`, no `System.Text.Json.Nodes` for payloads in `Njord.Weather`/`Njord.Messages`/`Njord.Pipeline`

## 2. Typed payloads, one kind at a time (golden masters must stay byte-identical)

- [ ] 2.1 Alerts: `AlertPayload` + derived detail records in `Payloads/`, `ToPayload()` in `Mapping/AnalysisPayloadMappings.cs`; mapper spec checks every member for all 14 alert types; `FromAlerts` uses it
- [ ] 2.2 Derived: `DerivedHorizonPayload`, `DerivedMetaPayload`, mappers (rounding 1 digit), specs; `FromDerived` uses them
- [ ] 2.3 Indices: `IndexDayPayload` with score envelopes and the day-0 frost/VPD members, mapper spec, `FromIndices`
- [ ] 2.4 Trends: `TrendPayload` with `TrendKey` derivation in one place (replaces inline `Replace`), converter for the parameter entries, mapper spec incl. all trend parameters
- [ ] 2.5 Consensus: `ConsensusHorizonPayload` with ordered parameter entries (`JsonKey`, `{JsonKey}_models`) then `_spread`/`_agreement`, converter, mapper spec (rounding 2 and 3 digits)
- [ ] 2.6 Forecast horizon: `HorizonProjection` in `Njord.Weather` returns typed entries (no `System.Text.Json.Nodes`), `ForecastHorizonPayload` + converter in `Njord.Mqtt`; adapt `StatePayloadBuilder.BuildPerHorizon`, `MqttStatePublisher`, `Njord.Weather.Tests/HorizonProjectionSpec.cs`, `Njord.Pipeline.Tests/PollPipelineSpec.cs`
- [ ] 2.7 `StatePayloadBuilder` is only mapper + serialize + topic; delete the `JsonObject` code
- [ ] 2.8 Proof: run `Njord.Mqtt.Tests`, `Njord.Weather.Tests`, `Njord.Pipeline.Tests` — all six golden masters unchanged (`git diff` shows none of the `*.verified.txt` modified)

## 3. Intentional wire corrections (BREAKING)

- [ ] 3.1 Null policy: enrichment payload members that can be absent serialize as explicit `null` (alerts gain `peak_value`, `hours_until`, `duration_hours` as null; check every nullable alert detail member); forecast horizon keeps omission; specs first
- [ ] 3.2 `soil_frozen` as JSON boolean in the ice alert payload; specs first
- [ ] 3.3 Update only the affected golden masters (alert snapshot), review each diff line by line
- [ ] 3.4 Add Verify snapshots per payload type and a roundtrip/shape spec (serialize then parse, assert member names and types)

## 4. Docs

- [ ] 4.1 `docs/mqtt-reference.md` and `docs/home-assistant/entities.md`: `soil_frozen` boolean, null rule per payload kind
- [ ] 4.2 `AGENTS.md` Decisions: MQTT payloads are typed records; null rule (forecast omits, enrichment explicit null)
- [ ] 4.3 Re-check that `D:\GIT\ha-njord` and `e2e/` do not read the changed MQTT fields (grep `soil_frozen`, alert timing fields)

## 5. Validation

- [ ] 5.1 `dotnet build Njord.slnx` and the sequential test loop from `AGENTS.md` (all projects)
- [ ] 5.2 `dotnet format` whitespace check and slopwatch
- [ ] 5.3 `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh`
- [ ] 5.4 Commit as `feat!:` (step 0 as its own `refactor:` commit first); the `Release-As: 0.5.0` footer is already on the release commits, do not add another
