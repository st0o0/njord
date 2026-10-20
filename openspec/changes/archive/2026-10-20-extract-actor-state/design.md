## Context

FunkArr pattern: actor holds `_state`, every handler does `_state = _state.Apply(msg); Sender.Tell(result)`. All domain logic testable without actors.

## Goals / Non-Goals

**Goals:** All actors follow thin-shell pattern. Pure state tests.
**Non-Goals:** Behavior changes (except GrpcConsumer bug fix).

## Decisions

### D1: GrpcConsumerState — immutable dict state
```csharp
sealed record GrpcConsumerState(
    ImmutableDictionary<(string Location, DomainKind), PipelineEvent> DomainData,
    ImmutableDictionary<string, ImmutableList<Alert>> Alerts)
{
    static readonly GrpcConsumerState Empty = new(...);
}
```
Extensions: `Apply(WeatherDataReceived)`, `Apply(AlertRaised)` (all alert types, fixing the bug), `GetLatestAlerts(location)`.

### D2: MqttConnectionState — connection lifecycle
Replace `_isConnected`, `_disconnectedSince`, `_connectAttempts`, `_onlineAttempts`, `_session` with a sealed record.

### D3: PollRoundState — in-flight round tracking
`_roundId`, `_domainResults`, `_pendingDomains` become a `PollRoundState?` on `PollWorkerState`. Null when no round in flight.

### D4: ProjectionEngine.Evaluate as static pure function
```csharp
static IReadOnlyList<PipelineEvent> Evaluate(
    DomainDataReceived data,
    Dictionary<string, (LocationForecast, LocationForecast?)> state,
    IReadOnlyList<IProjectionGroup> groups)
```
