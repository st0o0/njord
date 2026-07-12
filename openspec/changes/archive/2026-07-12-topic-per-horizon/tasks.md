## 1. Topic Scheme Update

- [x] 1.1 Update `src/Njord/Egress/TopicScheme.cs` — replace `StateTopic(baseTopic, location, model)` with `HorizonTopic(baseTopic, location, model, horizon)` returning `{baseTopic}/{slug(location)}/{model.Id}/{horizon}`. Add `LegacyStateTopic(baseTopic, location, model)` returning the old `{baseTopic}/{slug}/{model}/state` for tombstoning.
- [x] 1.2 Update `src/Njord.Tests/Egress/TopicSchemeSpec.cs` — add tests for new `HorizonTopic` (hourly + daily variants) and `LegacyStateTopic`

## 2. StatePayloadBuilder Per-Horizon

- [x] 2.1 Update `src/Njord/Egress/StatePayloadBuilder.cs` — add `BuildPerHorizon(ModelForecast, ResolvedParameterSet, IReadOnlyList<int>, int)` returning `Dictionary<string, string>` (key = horizon like "h3"/"d0", value = flat JSON). Keep the old `Build` temporarily for migration.
- [x] 2.2 Update `src/Njord.Tests/Egress/StatePayloadBuilderSpec.cs` — tests for `BuildPerHorizon`: correct keys (h3-h72, d0-d3), flat JSON structure (no nesting), correct parameter values per horizon, null/missing values omitted

## 3. Discovery Payload Update

- [x] 3.1 Update `src/Njord/Egress/DiscoveryPayloadBuilder.cs` — change each component's `state_topic` from the device-level state topic to the horizon-specific topic using `TopicScheme.HorizonTopic`; simplify `value_template` from `{{ value_json.h3.{key} }}` to `{{ value_json.{key} }}`
- [x] 3.2 Update `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` — verify components reference horizon topics and use simplified templates

## 4. Delta-Publishing in Pipeline

- [x] 4.1 Update `src/Njord/Pipeline/PipelineActor.cs` — replace single `Select(ToMqttMessage)` with fan-out: call `BuildPerHorizon`, compare each horizon's payload against a `ConcurrentDictionary<(string, string, string), string>` cache, emit `MqttMessage` only for changed horizons, update cache on emit
- [x] 4.2 Create `src/Njord.Tests/Pipeline/DeltaPublishingSpec.cs` — tests: first cycle publishes all horizons, unchanged horizon is skipped, changed horizon publishes and updates cache, actor restart (new instance) publishes all again

## 5. Legacy Tombstone

- [x] 5.1 Update `src/Njord/Egress/MqttEgressActor.cs` — on Connected, offer tombstone `MqttMessage(LegacyStateTopic(...), "", retain: true)` for each configured device into the tombstone queue (alongside stale device tombstoning)
- [x] 5.2 Update `src/Njord.Tests/Egress/MqttEgressActorSpec.cs` or integration test — verify legacy state topics are tombstoned on connect

## 6. Cleanup & Validation

- [x] 6.1 Remove old `StatePayloadBuilder.Build` (single-JSON variant) once all callers are migrated
- [x] 6.2 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 6.3 Run build: `dotnet build src/Njord.slnx`
- [x] 6.4 Run `dotnet slopwatch` from repo root
