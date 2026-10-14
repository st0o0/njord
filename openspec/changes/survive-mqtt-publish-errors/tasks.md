## 1. Failing specs

- [ ] 1.1 Add `src/Njord.Mqtt.Tests/MqttPublishFailureSpec.cs` (sealed, hosted TestKit, `[Fact(Timeout = TestTimeouts.Hosted)]`) with a scripted `IMqttTransport`: later events published after a failed publish; failed payload republished on the next cycle; failed publish keeps the pipeline subscription; hung publish times out; failure counted in `njord_mqtt_publish_failures_total`
- [ ] 1.2 Extend `src/Njord.Mqtt.Tests/TelemetrySpec.cs` with the new counter's name, unit and meter
- [ ] 1.3 Run the specs and record the failures

## 2. Implementation

- [ ] 2.1 Add `njord_mqtt_publish_failures_total` to `src/Njord.Mqtt/Telemetry.cs`
- [ ] 2.2 Add `PublishTimeout` (default 10 s) to `src/Njord.Mqtt/MqttEgressTuning.cs`
- [ ] 2.3 In `src/Njord.Mqtt/MqttStatePublisher.cs` inject `MqttEgressTuning`, move dedup into the publish step, record the hash only after a successful send, catch and log (Warning) + count failures and timeouts
- [ ] 2.4 Update `src/Njord.Mqtt.Tests/MqttStateSpec.cs` construction for the new constructor parameter

## 3. Validation

- [ ] 3.1 `dotnet build Njord.slnx` from `src/`: 0 warnings, 0 errors
- [ ] 3.2 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj` three times, all green
- [ ] 3.3 Full test loop from AGENTS.md green; update "Current total" in `AGENTS.md`
- [ ] 3.4 No `*.verified.*` changes
