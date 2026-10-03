---
name: njord-enrichment-feature
description: Use when adding or changing an enrichment feature (alerts, derived, trends, indices, history) — a compute-only IEnrichmentFeature in src/Njord.Enrichment, its IEnrichmentPresenter in src/Njord.Mqtt (device id, discovery payload, state messages), the type-name and options toggle in Njord.Core, DI registration and golden-master snapshots.
---

# njord enrichment feature

Rules (static entity set, `TimeProvider`, HA device cut, toggleable features) live in `AGENTS.md` — follow them. This skill is the checklist.

A feature is a **pair**: the feature *computes* (library `Njord.Enrichment`, references only `Njord.Core`), the presenter *presents* (library `Njord.Mqtt`, references only `Njord.Core`). They never reference each other; they meet through the `TypeName` string (`EnrichmentTypeNames` in Core) and the result record in `Njord.Domain`. `Njord.Architecture.Tests/ZoneArchitectureSpec.cs` enforces both directions.

## Feature: pick the interface (`src/Njord.Enrichment/`)

| Interface | Input | Use for | Example |
|---|---|---|---|
| `IStatelessEnrichment` | `Compute(consensus, sensors)` | Pure function of the current consensus | `Features/AlertEnrichment.cs` |
| `IStatefulEnrichment` | `Compute(consensus, previous, sensors)` | Needs the previous cycle's consensus (yield nothing when `previous` is null) | `Features/TrendEnrichment.cs` |
| `IActorEnrichment` | `CreateFlow(IUntypedActorContext)` -> `Flow<ModelSnapshot, EgressEvent, NotUsed>` | Owns an actor/long history | `Features/HistoryEnrichment.cs` (`ResolveChildActor<ForecastHistoryActor>`, `StreamSupervision.LoggingDecider`) |

All extend `IEnrichmentFeature`, which has exactly `TypeName` and `Enabled`. A feature has no MQTT surface.

## Presenter (`src/Njord.Mqtt/`)

`IEnrichmentPresenter` (`IEnrichmentPresenter.cs`): `TypeName`, `Enabled`, `DeviceId(location)`, `BuildDiscoveryPayload(DiscoveryContext ctx, location)`, `ToStateMessages(result, baseTopic, location)`. Implementations are `internal sealed` in `Presentation/` (`AlertPresenter.cs` is the simplest). `DiscoveryActor` and `MqttEgressActor` dispatch purely over the registered presenters by `TypeName`; `ConsensusPresenter` is the one presenter without a feature (consensus is computed by the pipeline).

## Checklist

1. **Options + type name (Core)** — add `XxxOptions` (with `bool Enabled`) in `src/Njord.Core/Configuration/` (or `src/Njord.Domain/Options/` when the domain needs it; namespace `Njord.Configuration` either way), a property on `EnrichmentOptions` and a case in `EnrichmentOptions.IsEnabled(string typeName)` (`src/Njord.Core/Configuration/EnrichmentOptions.cs`; unknown names throw — extend `src/Njord.Core.Tests/Configuration/EnrichmentOptionsValidationSpec.cs`), and a constant in `src/Njord.Core/Enrichment/EnrichmentTypeNames.cs`.
2. **Result type** — domain record in `src/Njord.Domain/Analysis/`; add it to the `EnrichmentTypes` map in `src/Njord.Grpc/EnrichmentSnapshotMapping.cs` so snapshots round-trip (and a case in `EnrichmentProtoMapper.MapToEvent` if gRPC exposes it), and refresh the Verify baselines (`njord-actor-spec`).
3. **Feature class** — `internal sealed class XxxEnrichment : I...Enrichment` in `src/Njord.Enrichment/Features/`; `TypeName => EnrichmentTypeNames.Xxx`; `_enabled = options.Value.Enrichment.IsEnabled(TypeName)` in the constructor; inject `TimeProvider` if time matters.
4. **Compute** — yield `new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result)`.
5. **Presenter** — `internal sealed class XxxPresenter : IEnrichmentPresenter` in `src/Njord.Mqtt/Presentation/`, same `TypeName`/`Enabled` source (`IsEnabled`). `DeviceId(location) => TopicScheme.EnrichmentDeviceId(location, TypeName)`; discovery components are built from config/enums, never from data (inject `IOptions<NjordOptions>` for horizons/parameters/preferences); `expire_after = 2 x poll interval` (`ctx.PollInterval`); availability via `TopicScheme.AvailabilityTopic`; envelope via `DiscoveryPayloadBuilder.BuildDeviceEnvelope(...)`.
6. **State messages** — add `StatePayloadBuilder.FromXxx(result, baseTopic)` in `src/Njord.Mqtt/StatePayloadBuilder.cs` and call it from `ToStateMessages`. Topics come from `TopicScheme` (`src/Njord.Mqtt/TopicScheme.cs`).
7. **Register both** — feature in `AddNjordEnrichment` (`src/Njord.Enrichment/EnrichmentServiceCollectionExtensions.cs`: `services.AddSingleton<IEnrichmentFeature, XxxEnrichment>();`), presenter in `AddNjordMqtt` (`src/Njord.Mqtt/MqttServiceCollectionExtensions.cs`, after `ConsensusPresenter`: registration order is the publish order). `EnrichmentActor` picks features up via `OfType<I...>().Where(f => f.Enabled)`; no host wiring.
8. **Tests** — feature spec `src/Njord.Tests/Enrichment/Features/XxxEnrichmentSpec.cs`; presenter/golden-master specs in `src/Njord.Tests/Mqtt/`: add the type to `EnrichmentDiscoverySnapshotSpec` and `EnrichmentStateSnapshotSpec` (Verify; inputs in `EnrichmentGoldenMasterFixtures.cs`) and approve the new `.verified.txt` files (never edit existing ones: they pin the wire format). `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` asserts the feature/presenter parity (presenter set = feature set + `consensus`), unique kebab-case type names, and that `consensus` is **not** a feature — update its counts.

## Worked example — stateless feature + presenter (from `AlertEnrichment` / `AlertPresenter`)

```csharp
// src/Njord.Enrichment/Features/AlertEnrichment.cs
internal sealed class AlertEnrichment : IStatelessEnrichment
{
    public string TypeName => EnrichmentTypeNames.Alerts;
    public bool Enabled => _enabled;
    // ctor: _alertOptions = options.Value.Enrichment.Alerts; _enabled = ...IsEnabled(TypeName);

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
    {
        var result = AlertEvaluator.EvaluateAll(consensus, _alertOptions, _timeProvider);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }
}

// src/Njord.Mqtt/Presentation/AlertPresenter.cs
internal sealed class AlertPresenter : IEnrichmentPresenter
{
    public string TypeName => EnrichmentTypeNames.Alerts;
    public string DeviceId(string location) => TopicScheme.EnrichmentDeviceId(location, TypeName);
    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromAlerts((AlertResult)result, baseTopic);
    // BuildDiscoveryPayload: components per AlertType, expire_after, availability (see AlertPresenter.cs)
}
```

## Pitfalls

- Consensus is deliberately **not** a feature (it is emitted by `EnrichmentActor` itself); its presentation is `ConsensusPresenter`.
- A feature's entities depend on config (`Enabled`), never on what a poll returned; missing values are `unavailable` states, not missing entities.
- Never reference `Njord.Mqtt` from `Njord.Enrichment` or the reverse; share only through Core/Domain types.
- Sensor input comes from the `SensorSnapshot` argument (SensorHub latest values); do not call the hub from a feature.
