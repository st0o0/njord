## Context

The survey showed: no Kestrel/`TestServer` host in tests, no `Program` entry testing, in-memory Akka persistence only, and `MqttNetPublisher` covered by 3 static tests. FunkArr's `IntegrationTests` builds a `WebApplication` by hand with `TestServer`; its own survey notes `WebApplicationFactory` is referenced but unused and the production setup containers are therefore not exercised. njord can do better: its host is composed from setup containers, so `WebApplicationFactory<Program>` tests the real composition.

## Goals / Non-Goals

**Goals:** catch wiring, serialization, protocol and persistence errors that fakes hide; keep tests deterministic and bounded.

**Non-Goals:** testing Home Assistant or ha-njord (the e2e plan covers that), load testing, testing the Docker image or multi-arch builds, replacing existing in-process specs.

## Decisions

1. **One host fixture per test class, factory-based.** `NjordHostFactory : WebApplicationFactory<Program>` builds the host with `UseSetting`/in-memory configuration (unique temp directory per instance, `Njord:Storage:PersistencePath` and `Njord:Persistence` pointing there), replaces `IOpenMeteoClient` with `FakeOpenMeteoClient` from `Njord.Tests.Shared`, and uses `FakeTimeProvider` where the test advances time. `Program` becomes `public partial class Program;` (top-level statements need the partial for the factory); the existing `InternalsVisibleTo Njord.IntegrationTests` already in the host project is kept.
2. **Kestrel dual port is not exercised.** `TestServer` serves one in-memory endpoint for HTTP/1.1 and HTTP/2; the port split (8080/8081) stays covered by configuration specs. This is stated in the docs of the project so nobody assumes otherwise.
3. **Restart tests use two factories on one directory.** The first factory is disposed (graceful actor system shutdown flushes persistence) before the second starts; the assertion goes through the gRPC channel, the public behaviour, not through actor internals.
4. **Testcontainers for MQTT only.** A pinned `eclipse-mosquitto:2` tag (image digest recorded) with a generated config allowing anonymous access. The observer is a plain MQTTnet client from the existing package; no new broker abstractions. The container is shared per test class through `IAsyncLifetime`, and per-test topics use the unique base topic setting so tests are independent.
5. **No dynamic skip when Docker is absent.** The broker tests throw a descriptive exception in the fixture initialization, matching the repo's rule against disabled tests. The namespace split lets developers filter them (`-- -namespace Njord.IntegrationTests.Http` for the Docker-free part).
6. **Time and waits.** Waits use `AwaitAssert`-style bounded helpers with `TestTimeouts`; real broker round trips use `RealTimeWaitMax`. xUnit timeouts follow the existing margin rule; the parallelization settings from `xunit.runner.json` apply, and fixtures are collection-scoped to bound host and container counts.
7. **Golden master reuse.** The retained-payload test publishes the `AnalysisFixture` events through the real transport and compares with the existing MQTT golden-master files rather than adding new snapshots.
8. **CI.** The solution already runs through `dotnet test --solution` on ubuntu runners with Docker. The additional runtime is measured on the first run; if the 15-minute job limit is at risk, the shared workflow's timeout is raised in `st0o0/github-workflows` (not changeable here).

## Risks / Trade-offs

- Container start adds 5 to 15 seconds per fixture; mitigated by sharing the container per class.
- Developers without Docker cannot run the broker tests; filterable and documented.
- `WebApplicationFactory` with top-level statements needs the partial class; the one-line production change is the only seam added.
- SQLite file locking on Windows during restart tests: each lifetime fully disposes before the next opens; the temporary directory is removed in `DisposeAsync`.

## Migration Plan

No operator impact. Order: project skeleton and host smoke, gRPC channel tests, restart/persistence tests, broker tests, CI timing check and documentation.
