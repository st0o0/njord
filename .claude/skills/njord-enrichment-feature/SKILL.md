---
name: njord-enrichment-feature
description: Use when adding or changing an enrichment feature (alerts, derived, trends, indices, history) — an IEnrichmentFeature under src/Njord/Enrichment/Features, its options toggle, DI registration, discovery payload and MQTT state messages.
---

# njord enrichment feature

Rules (static entity set, `TimeProvider`, HA device cut, toggleable features) live in `AGENTS.md` — follow them. This skill is the checklist.

## Pick the interface (`src/Njord/Enrichment/`)

| Interface | Input | Use for | Example |
|---|---|---|---|
| `IStatelessEnrichment` | `Compute(consensus, sensors)` | Pure function of the current consensus | `Features/AlertEnrichment.cs` |
| `IStatefulEnrichment` | `Compute(consensus, previous, sensors)` | Needs the previous cycle's consensus (yield nothing when `previous` is null) | `Features/TrendEnrichment.cs` |
| `IActorEnrichment` | `CreateFlow(IUntypedActorContext)` → `Flow<ModelSnapshot, EgressEvent, NotUsed>` | Owns an actor/long history | `Features/HistoryEnrichment.cs` (`ResolveChildActor<ForecastHistoryActor>`, `StreamSupervision.LoggingDecider`) |

All extend `IEnrichmentFeature`: `TypeName`, `Enabled`, `DeviceId(location)`, `BuildDiscoveryPayload(ctx, location)`, `ToStateMessages(result, baseTopic, location)`.

## Checklist

1. **Options** — add `XxxOptions` (with `bool Enabled`) in `src/Njord/Configuration/` and a property on `EnrichmentOptions` (`Configuration/EnrichmentOptions.cs`).
2. **Result type** — domain record in `src/Njord/Domain/Analysis/`; add it to the `EnrichmentTypes` map in `src/Njord/Persistence/EnrichmentSnapshotDtos.cs` so snapshots round-trip, and refresh the Verify baseline (`njord-actor-spec`).
3. **Feature class** — `internal sealed class XxxEnrichment : I…Enrichment` in `Enrichment/Features/`; `TypeName` is a kebab-case constant; read `Enabled` from options in the constructor; inject `TimeProvider` if time matters.
4. **Compute** — yield `new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result)`.
5. **Discovery** — `BuildDiscoveryPayload`: one device per feature via `DeviceId(location) => TopicScheme.EnrichmentDeviceId(location, TypeName)`; components built from config/enums, never from data; `expire_after = 2 × poll interval`; availability via `TopicScheme.AvailabilityTopic`; envelope via `DiscoveryPayloadBuilder.BuildDeviceEnvelope(...)`.
6. **State messages** — add `StatePayloadBuilder.FromXxx(result, baseTopic)` in `src/Njord/Mqtt/StatePayloadBuilder.cs` and call it from `ToStateMessages`. Topics come from `TopicScheme` (`src/Njord/Mqtt/TopicScheme.cs`).
7. **Register** — `services.AddSingleton<IEnrichmentFeature, XxxEnrichment>();` in `src/Njord/Configuration/NjordServiceSetup.cs` (next to the existing five). `EnrichmentActor` picks features up via `OfType<I…>().Where(f => f.Enabled)`; no further wiring.
8. **Tests** — `src/Njord.Tests/Enrichment/Features/XxxEnrichmentSpec.cs` and update the counts/type names in `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` (it asserts the feature count, unique kebab-case type names, and that `consensus` is **not** a feature).

## Worked example — stateless feature (from `AlertEnrichment`)

```csharp
internal sealed class AlertEnrichment : IStatelessEnrichment
{
    private readonly AlertOptions _alertOptions;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enabled;

    public string TypeName => "alerts";
    public bool Enabled => _enabled;

    public AlertEnrichment(IOptions<NjordOptions> options, TimeProvider timeProvider)
    {
        _alertOptions = options.Value.Enrichment.Alerts;
        _timeProvider = timeProvider;
        _enabled = options.Value.Enrichment.Alerts.Enabled;
    }

    public string DeviceId(string location) => TopicScheme.EnrichmentDeviceId(location, TypeName);

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
    {
        var result = AlertEvaluator.EvaluateAll(consensus, _alertOptions, _timeProvider);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }

    public IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location)
        => StatePayloadBuilder.FromAlerts((AlertResult)result, baseTopic);
    // BuildDiscoveryPayload: see AlertEnrichment.cs (components per AlertType, expire_after, availability)
}
```

## Pitfalls

- Consensus is deliberately **not** a feature (it is emitted by `EnrichmentActor` itself).
- A feature's entities depend on config (`Enabled`), never on what a poll returned; missing values are `unavailable` states, not missing entities.
- Sensor input comes from the `SensorSnapshot` argument (SensorHub latest values); do not call the hub from a feature.
