## Why

njord's `Program.cs` wires all DI registrations, Akka configuration, persistence HOCON,
and actor creation inline in a single file. Actor registration uses the verbose
`registry.Register<T>(system.ActorOf(resolver.Props<T>(), name))` pattern repeatedly.
Actors resolve peers via raw `ActorRegistry` lookups instead of typed helpers.

Servus.Core's `SetupContainer` pattern and Servus.Akka's `ActorSystemSetupContainer` /
`WithResolvableActors` / `ResolveChildActor` / `GetActor<T>` extensions already solve
these problems in other projects (Akka.Streams.Http). Adopting them now — before more
actors and consumers land — keeps the bootstrap modular, testable, and consistent with
the team's other Akka.NET codebases.

## What Changes

- Add `Servus` (Servus.Core) and `Servus.Akka` NuGet packages.
- Replace monolithic `Program.cs` with composable `SetupContainer` modules:
  `NjordServiceSetup`, `NjordActorSystemSetup`, and application-level setup.
- Replace `.WithActors((system, registry, resolver) => ...)` with
  `WithResolvableActors` for declarative actor registration.
- Replace manual `ActorRegistry` lookups in actors with `context.GetActor<T>()`
  and `context.ResolveChildActor<T>()` from Servus.Akka.
- Introduce `AppBuilder` entry point orchestrating the setup containers.

## Non-goals

- Changing any actor behavior, message contracts, or stream topology.
- Migrating persistence backend (PostgreSQL is a separate change).
- Migrating tests to Akka.TestKit (separate change).
- Adding new features or actors.

## Capabilities

### New Capabilities

- `servus-bootstrap`: Composable application bootstrap using Servus.Core
  SetupContainers and Servus.Akka ActorSystemSetupContainer, replacing the
  monolithic Program.cs wiring.

### Modified Capabilities

- `service-configuration`: Actor registration moves from inline `.WithActors`
  to `WithResolvableActors`; actors use Servus.Akka resolve/registry extensions
  instead of raw `ActorRegistry`.

## Impact

- **Packages:** Two new NuGet dependencies (`Servus`, `Servus.Akka`) added to
  `Directory.Packages.props` and the `Njord` project.
- **Program.cs:** Rewritten to use `AppBuilder` pipeline; no more inline DI or
  HOCON blocks.
- **Actors:** All actors that inject `ActorRegistry` switch to Servus.Akka
  typed extensions (`GetActor<T>`, `ResolveChildActor<T>`).
- **DI extensions:** `AddOpenMeteoIngest()` and `AddMqttEgress()` move into
  a `IServiceSetupContainer` implementation; their public API is preserved.
- **Tests:** No test changes (actors, DI registrations, and behavior are
  unchanged; only wiring moves).
- **API budget:** No impact — no polling changes.
