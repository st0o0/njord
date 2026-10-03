## 0. Reproducers (red first, from `src/`)

Load shell helper used below (stop it afterwards and confirm with `pgrep -af "while :"` that nothing is left):
`for i in $(seq 1 7); do (while :; do :; done) & echo $! >> /tmp/load.pids; done` ... `kill $(cat /tmp/load.pids); rm /tmp/load.pids`

- [x] 0.1 Build once (`dotnet build Njord.slnx`), then record baseline: `taskset -c 0 dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build -- -class "<FQN>"` for the classes in sections 1-5; expected red before the fix: `SchedulerActorGetPollStatesSpec`, `ActorKeyRegistrationSpec`, `HealthEndpointSpec` (reproduced 2/2 at measurement time)
- [x] 0.2 Record baseline full-suite under load (7 busy loops): 4 runs, expect 4 red (5-6 failures each at measurement time)

## 1. Timeout vs Akka wait (cause A)

- [x] 1.1 Add `TestTimeouts` constants to `src/Njord.Tests.Shared` (hosted-spec outer timeout, inner `AwaitAssert` bound)
- [x] 1.2 `Pipeline/SchedulerActorGetPollStatesSpec`: use the shared timeout; red under `taskset -c 0` first, then green
- [x] 1.3 `Pipeline/SchedulerActorGetPollStatesBeforeReadySpec`: same
- [x] 1.4 `Configuration/ActorKeyRegistrationSpec`: explicit `AwaitAssertAsync` `max:` below the outer timeout; same red/green check
- [x] 1.5 `Pipeline/SchedulerActorSnapshotSpec`: widen outer timeout to cover 51 asks plus restart; same check
- [x] 1.6 `Pipeline/SchedulerActorStartupOrderSpec`: widen outer timeout over `AwaitCondition` + `Ask`; same check

## 2. Name-reuse race (cause C)

- [x] 2.1 `Pipeline/BudgetTrackerActorSpec.Recovery_skips_events_from_previous_month`: red first by a loop that stops and recreates the same name 200 times (temporary local check, not committed) to show `InvalidActorNameException`; then use a unique name for the recovered actor and confirm persistence id `budget-tracker` still drives recovery

## 3. Retry backoff (cause B)

- [x] 3.1 Decide hook vs options (design decision 4; chosen: HOCON override `njord.retry-backoff.override` read by `RetryBackoff.For`, because the production actors are sealed and cannot take a virtual hook); add the injectable delay to `Njord.Core/Actors/StreamConsumerActor.cs` and `SchedulerActor` with production default `min(2^n, 30)` s
- [x] 3.2 Pure characterization spec for the default delay sequence (1, 2, 4, 8, 16, 30, 30 s)
- [x] 3.3 `Egress/ModelStateActorSpec`: override delay to ~10 ms, drop the 4 s waits, keep failure-handling assertions
- [x] 3.4 `Mqtt/MqttEgressActorSpec`: same
- [x] 3.5 `Mqtt/DiscoveryActorSpec`: same
- [x] 3.6 `Grpc/GrpcSnapshotConsumerTerminatedSpec`: same
- [x] 3.7 `Pipeline/SchedulerActorRefFailureSpec`: same
- [x] 3.8 Check `Actors/StreamConsumerActorSpec`, `Enrichment/EnrichmentActorSpec`, `Pipeline/PipelineConnectionSpec`, `Pipeline/SchedulerActorSpec` for remaining real-time waits above 1 s and apply the same treatment

## 4. HTTP host startup (cause D)

- [x] 4.1 `Health/HealthEndpointSpec`: warm-up in `InitializeAsync`, shared outer timeout; red first under `taskset -c 0`, then green

## 5. Acceptance

- [x] 5.1 Re-run all `taskset -c 0` class reproducers from 0.1: all green, twice each
- [x] 5.2 Re-run 4 full-suite runs under 7 busy loops: 0 failures (any new failing test gets its own cause analysis and task here before proceeding)
- [x] 5.3 30 consecutive full-suite runs with 0 failures on an otherwise idle machine; about 35 minutes (≈65 s per run). Run from `src/` after one `dotnet build Njord.slnx`: `for i in $(seq 1 30); do dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build > /tmp/stab-$i.log 2>&1 || echo "run $i FAILED"; done`
- [x] 5.4 `dotnet slopwatch` is not installed in this checkout (no tool manifest), so checked manually via the diff: no `Skip`, retry attribute or deleted test in the diff

## 6. Validation commands

- `openspec validate stabilize-load-sensitive-tests`
- `cd src && dotnet build Njord.slnx`
- `cd src && dotnet run --project Njord.Tests/Njord.Tests.csproj` (unit + actor tests; never `dotnet test`)
- Single class: `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.SchedulerActorGetPollStatesSpec"`
- Single-core stress: `taskset -c 0 dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build -- -class "<FQN>"`
- API budget: 0 requests (no Open-Meteo traffic; tests only)
