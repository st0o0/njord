## 1. Failing specs

- [ ] 1.1 Add `src/Njord.Mqtt.Tests/MqttConnectionLifecycleSpec.cs` (sealed, hosted TestKit, `[Fact(Timeout = TestTimeouts.Hosted)]`) with MQTTnet-faithful fakes (connect while connected is rejected, a failed connect also signals a disconnect, scripted subscribe/online/offline failures): failed online publish retried on the same connection; failed online publish does not restart the actor (listener survives, no `offline`, no extra connect); hung online publish times out and is retried; failed status subscribe keeps the connection; failed connect schedules exactly one reconnect; disconnect timestamp survives failed reconnects; stop publishes `offline`; stop completes when the `offline` publish fails
- [ ] 1.2 Add `src/Njord.Mqtt.Tests/MqttNetPublisherSpec.cs` (sealed, plain `[Fact]`): a non-success CONNACK result throws naming the result code; a success result passes; only a disconnect of an established connection is reported
- [ ] 1.3 Run the specs against the current code and record the failures

## 2. Implementation

- [ ] 2.1 `src/Njord.Mqtt/Transport/MqttNetPublisher.cs`: `EnsureAccepted` for the CONNACK result, `IsLostConnection` filter on the disconnected handler
- [ ] 2.2 `src/Njord.Mqtt/MqttConnection.cs`: Become state machine (`Connecting`, `WaitingToReconnect`, `Connected`) with `IWithTimers` (single `reconnect` and `online-retry` timers) and `IWithUnboundedStash` for `Disconnected` during a connect
- [ ] 2.3 Subscribe and `online` publish via `PipeTo` with session-tagged success/failure records; `online` bounded by `PublishTimeout`; capped exponential online retry; Warnings for failures
- [ ] 2.4 Health snapshot: `(false, now)` only when an established connection is lost; best-effort, observed `offline` publish in `PostStop`

## 3. Validation

- [ ] 3.1 `dotnet build Njord.slnx` from `src/`: 0 warnings, 0 errors
- [ ] 3.2 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj` three times, all green
- [ ] 3.3 Full test loop from AGENTS.md green; update "Current total" in `AGENTS.md`
- [ ] 3.4 No `*.verified.*` changes; `openspec validate harden-mqtt-connection-lifecycle --no-interactive`
