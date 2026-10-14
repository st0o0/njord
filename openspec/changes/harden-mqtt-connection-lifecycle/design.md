## Context

See proposal.md - Why. `MqttConnection` is a `ReceiveActor` registered via `WithSingleton`, so it
runs under the singleton manager's default supervisor strategy (one-for-one, restart on any
exception, unlimited retries). Its collaborators `IMqttConnection` and `IMqttTransport` are both the
DI singleton `MqttNetPublisher`, which wraps one MQTTnet 5 `IMqttClient` and attaches the
`DisconnectedAsync` / message handlers only on the first connect. `MqttStatePublisher` publishes
state through the same transport but never talks to the connection actor.

### Lifecycle audit (before this change)

| # | Call / signal | Where | Failure handling today | Effect of a failure |
|---|---|---|---|---|
| 1 | `IMqttConnection.ConnectAsync` | `Connect()` from `PreStart` / `Reconnect` | `PipeTo` → `ConnectFailed` → `ScheduleReconnect` | Handled. But MQTTnet also raises `DisconnectedAsync` (`ClientWasConnected = false`) for a failed connect, so each failure schedules two reconnects; extra timers race the next connect (`"Not allowed to connect while connect/disconnect is pending"`, `"... after the connection is established"`) and keep the loop running after success. |
| 2 | Non-success CONNACK (bad credentials, not authorized) | `MqttNetPublisher.ConnectAsync` | Result ignored; task completes successfully | Actor treats the connection as established, the `online` publish throws `MqttClientNotConnectedException`, actor crashes; restart connects again immediately → tight crash/restart loop without backoff. |
| 3 | `IMqttConnection.SubscribeAsync({DiscoveryPrefix}/status)` | awaited in `ReceiveAsync<Connected>` | `try/catch` → Warning | No crash, but blocks the mailbox while pending (no timeout). |
| 4 | `IMqttTransport.SendAsync(status, "online")` | awaited in `ReceiveAsync<Connected>` | **none** | Throw → actor crash → restart: `PostStop` publishes retained `offline` on the live connection, `PreStart` connects on the connected client → rejected forever (verified: 21 × `"It is not allowed to connect with a server after the connection is established."` in one spec run); inbound listeners lost; health stays `IsConnected = true`. Hang → mailbox blocked indefinitely (no timeout). |
| 5 | `Disconnected` signal | `Receive<Disconnected>` | always: health `(false, now)` + `ScheduleReconnect` | Also fires for failed connects (row 1): resets `DisconnectedSince` on every failed attempt, so the health check cannot reach `Unhealthy` while attempts fail within 2 min; duplicates reconnect timers. |
| 6 | `SendAsync(status, "offline")` | `PostStop` | fire-and-forget, task discarded | Failure is an unobserved task exception; on a restart it publishes `offline` while still connected. |
| 7 | Inbound HA status message | `Inbound` → listeners | no transport call | Not affected; only lost (listeners) on restart. |
| 8 | `MqttNetPublisher.DisposeAsync` (disconnect) | DI container disposal | `try/catch` → Debug | Not actor-owned; already safe. |

`MqttStatePublisher` is not affected by a connection-actor restart (it holds the transport, not the
actor) and already survives failed publishes (`survive-mqtt-publish-errors`). The health snapshot is
affected as listed in rows 4 and 5.

## Goals / Non-Goals

**Goals:**
- No transport failure path in `MqttConnection` throws inside a handler or awaits inside a handler.
- Retained availability converges to `online` whenever a connection is established.
- One reconnect timer at most; health snapshot describes the real connection state.

**Non-Goals:**
- Changing the supervisor strategy as a safety net (the actor must not rely on restarts at all).
- Detecting a broker that silently drops publishes while MQTTnet reports connected.

## Decisions

### Become state machine with three behaviors

`Connecting` (a connect is in flight), `WaitingToReconnect` (timer pending) and `Connected`.
`SubscribeInbound`, `Terminated` and `Inbound` are handled in every behavior. A `Disconnected`
signal is stashed while a connect is in flight (it may belong to the connection that is just being
established) and unstashed after the outcome is known; in `WaitingToReconnect` it is ignored; only
in `Connected` does it update health and schedule a reconnect.
Alternative: a boolean `_connected` field plus guards in one behavior - rejected, it hides which
messages are valid when and is how the duplicate-timer bug crept in.

### Single reconnect timer via `IWithTimers`

Reconnects and online retries use `Timers.StartSingleTimer` with fixed keys (`reconnect`,
`online-retry`), so scheduling again replaces a pending timer and leaving `Connected` cancels the
online retry. Alternative: keep `ScheduleTellOnceCancelable` and track the `ICancelable` - same
effect, more code.

### All transport calls via `PipeTo` with project-owned messages

`ConnectFailed(Exception)`, `StatusSubscribeFailed(int Session, Exception)`,
`OnlinePublishFailed(int Session, Exception)` plus success records (`Connected`,
`StatusSubscribed(int)`, `OnlinePublished(int)`). Results carry a session number that increments on
every established connection, so a late result from a previous connection is ignored instead of
triggering a retry on the new one. Each failure message is handled in every behavior in which it
can arrive (late results in `WaitingToReconnect`/`Connecting` are logged at Debug and dropped), so
nothing lands in dead letters. The `online` publish is wrapped in
`SendAsync(...).WaitAsync(PublishTimeout)` with a linked cancellation token, matching the state
publisher.

### Online retry: on the same connection, capped exponential backoff, unbounded count

Options considered:
- *Next reconnect only* - leaves `offline` retained for as long as the connection holds (hours),
  every entity unavailable in Home Assistant while njord is healthy. Rejected.
- *Immediately once* - a transient broker hiccup can outlast one retry; same failure mode. Rejected.
- *Force a reconnect* - tears down a working connection and its state subscriptions for a single
  failed publish; the reconnect would publish `online` anyway, but costs a full CONNECT cycle and
  briefly disconnects the state publisher. Rejected.
- **Chosen:** retry on the live connection after `ReconnectDelay × 2^(n-1)`, capped at ×64 (same
  formula as reconnects, so 5 s, 10 s, ... 320 s by default), until it succeeds or the connection is
  lost (the reconnect path publishes `online` again). No count bound: a bound would leave the
  retained status `offline` while connected, exactly what the retry exists to avoid. The cost is at
  most one tiny retained publish per 5 minutes per failed connection.

### Status-topic subscribe: Warning, no retry until the next connect

The inbound HA status stream has no production consumer today; a retry loop would add state for no
observable benefit. It no longer blocks the mailbox and is repeated on every reconnect.

### `MqttConnected` sent on connect, before the online publish

Listeners learn about the connection even when the `online` publish fails. Previously they were
notified only after a successful publish (and never after a failure).

### Health snapshot

`(true, null)` on every established connection (unchanged). `(false, now)` only on the transition
`Connected → WaitingToReconnect` caused by a lost connection. Failed connects never touch the
snapshot, so `DisconnectedSince` is the time of the loss. A failed `online` publish leaves the
snapshot at connected: the broker connection is up, which is what the `mqtt-connection` check
reports.

### `MqttNetPublisher` adapter fixes

- `ConnectAsync` throws (via an internal `EnsureAccepted(MqttClientConnectResult)` helper) when the
  result code is not `Success`, naming the result code and reason, so a refused CONNECT goes through
  `ConnectFailed` and the backoff.
- The `DisconnectedAsync` handler calls `onDisconnected` only when `ClientWasConnected` is true
  (internal `IsLostConnection` helper), so a failed connect raises one signal (`ConnectFailed`), not
  two. The actor stays robust against stray signals anyway (stash/ignore above).

### Best-effort `offline` on stop

`PostStop` cannot await; it starts a static local `async` helper that awaits the publish (bounded by
`PublishTimeout`) inside `try/catch` and logs a failure at Warning, so the task exception is always
observed. No `ContinueWith`.

## Risks / Trade-offs

- [Stashed `Disconnected` belongs to an older attempt] → only reachable without the adapter filter;
  the effect is one extra reconnect of a fresh connection, never a crash or duplicate timers.
- [Online retry spins against a broker that accepts CONNECT but never acks publishes] → capped at
  one attempt per 320 s, each bounded by the publish timeout; Warnings make it visible.
- [Late session results] → session number discards them.
- [Tests depend on real time for backoff] → `FastTuning` (50 ms delay, 200 ms publish timeout) keeps
  the lifecycle spec fast; negative waits use `ExpectNoMsg` on a probe.

## Migration Plan

No configuration or wire change. Deploy as usual; rollback is a plain revert.
