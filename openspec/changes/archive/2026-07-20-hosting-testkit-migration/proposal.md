## Why

Actor tests manually wire DI dependencies (`Options.Create(...)`, `NullLogger.Instance`, `ActorRegistry.For(Sys)`) instead of using `Akka.Hosting.TestKit` which provides a real DI container matching production startup. This has led to a 270-line `TestableSchedulerActor` clone that already diverges from the production actor (missing `ScheduleNext` for two failure reasons), and repeats the same boilerplate across 16 actor test classes.

## What Changes

- Replace `Akka.TestKit.Xunit.TestKit` base class with `Akka.Hosting.TestKit` for all actor test classes that don't need persistence interception
- Replace `PersistenceTestKit` base class with `Akka.Hosting.TestKit` + in-memory persistence config for persistence actor tests that don't use journal/snapshot interception APIs
- Keep `PersistenceTestKit` only for the 2 recovery specs that use `WithSnapshotLoad(load => load.Fail())`
- Delete the `TestableSchedulerActor` clone — test the real `SchedulerActor` via DI with short `DiscoveryInterval`
- Wire actor dependencies through `ConfigureServices` / `ConfigureAkka` overrides instead of manual construction

## Non-goals

- Consolidating or sharing fake actors across test classes — they are context-specific and short
- Changing pure unit tests (no base class) — they don't touch ActorSystem
- Migrating the `HealthEndpointSpec` (WebApplicationFactory) — unrelated pattern
- Adding new test coverage — this is a refactor of existing tests only

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `test-project-structure`: Actor test base class requirements change from `TestKit`/`PersistenceTestKit` to `Akka.Hosting.TestKit` as the primary base, with `PersistenceTestKit` retained only for interception scenarios

## Impact

- **Dependencies**: Add `Akka.Hosting.TestKit` package to `Njord.Tests.csproj` and `Directory.Packages.props`
- **Test classes affected**: 14 of 16 actor test classes migrate base class; 2 recovery specs stay on `PersistenceTestKit`
- **Deleted code**: `TestableSchedulerActor` (~270 lines) in `SchedulerActorSpec.cs`
- **No production code changes** — only test infrastructure
