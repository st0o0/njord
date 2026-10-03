## 1. ForecastHistoryActorSpec (2 usages)

- [x] 1.1 Replace `AsyncAssert.WaitUntil` in `Record_and_query_returns_persisted_data` with TestProbe or direct Ask retry.
- [x] 1.2 Replace `AsyncAssert.WaitUntil` in `Multiple_records_accumulate` with TestProbe or direct Ask retry.

## 2. SchedulerActorSpec (3 usages)

- [x] 2.1 Replace `AsyncAssert.StaysTrue` in `Rate_limited_failure_enforces_minimum_delay` with `ExpectNoMsg`.
- [x] 2.2 Replace `AsyncAssert.StaysTrue` in `Model_unavailable_does_not_trigger_retry` with `ExpectNoMsg`.
- [x] 2.3 Replace `AsyncAssert.StaysTrue` in `Malformed_payload_does_not_trigger_retry` with `ExpectNoMsg`.

## 3. ModelStateActorSpec (5 usages)

- [x] 3.1 Replace all `AsyncAssert.WaitUntil` calls with TestProbe-based assertions. Route egress events to a TestProbe instead of a shared list.

## 4. MqttConnectionActorSpec (4 usages)

- [x] 4.1 Replace all `AsyncAssert.WaitUntil` calls with TestProbe-based assertions.

## 5. DiscoveryActorSpec (13 usages)

- [x] 5.1 Replace all `AsyncAssert.WaitUntil` calls with TestProbe-based assertions. Route MQTT messages to a TestProbe instead of a probe actor with Ask.
- [x] 5.2 Replace `AsyncAssert.StaysTrue` with `ExpectNoMsg`.

## 6. SinkRefConnectionSpec (1 usage)

- [x] 6.1 Replace `AsyncAssert.WaitUntil` in `Persistent_actor_with_recovery_events_can_offer_via_sinkref` with direct await.

## 7. Cleanup

- [x] 7.1 Remove `AsyncAssert` class from `Njord.Tests.Shared` if no remaining usages, or mark remaining methods with a comment explaining why they're kept.

## 8. Validation

- [x] 8.1 Run `dotnet build Njord.slnx` from `src/` and confirm clean build.
- [x] 8.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` and confirm all tests pass.
