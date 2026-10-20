# CLAUDE.md

@AGENTS.md

## Workflow

Changes go through OpenSpec: `/opsx:explore` to think - `/opsx:propose` to create
a change (proposal/design/specs/tasks) - `/opsx:apply` to implement - `/opsx:archive`.
Implementation is TDD.

## Language

All code, specs, docs, and communication in English.

## Skill routing

### Project skills (njord-specific)

- `njord-enrichment-feature` -- **Load before adding/changing an enrichment.**
  `IDomainHandler`, `IFeature` name/`FeatureDescriptor`, registration, event/contract, Config Builder.
- `njord-actor-spec` -- **Load before writing actor, handler or persistence-record
  tests.** `NjordActorSpec`, in-memory persistence, `FakeTimeProvider`, Verify.
- `e2e-test` -- Full-stack check (njord + ha-njord + Home Assistant) via Docker Compose.

### Akka.NET skills -- when to load

**Always load the relevant skill before writing Akka code.** Requires the `akka-skills`
plugin, which is not installed on every machine; the routes do not resolve without it.

| Task | Load these skills |
|------|-------------------|
| New actor (any type) | `akka-skills:actor-state` |
| Actor with persistence | + `akka-skills:persistence` |
| Sharded entity / Singleton | + `akka-skills:cluster-hosting` |
| Actor with timers, PipeTo, DeathWatch | + `akka-skills:advanced-patterns` |
| Become/Unbecome state machine | + `akka-skills:become-state-machines` |
| Akka.Streams pipeline, hubs, StreamRefs | + `akka-skills:streams` |
| Supervision / BackoffOnFailure | + `akka-skills:supervision` |
| Actor pools / routing | + `akka-skills:actor-pools` |
| New message / command / query | `akka-skills:messages` (Pattern A: `Njord.Messages`) |
| Persistence records / events | `akka-skills:persistence` |
| Persistence infrastructure setup | `akka-skills:persistence-setup` |
| Actor tests | `njord-actor-spec` + `akka-skills:testing` |
| Servus setup containers | `akka-skills:setup-container` |
| Logging in actors | `akka-skills:logging` |
| Solution structure questions | `akka-skills:project-structure` |

### Plugin skills

- Akka.NET: `dotnet-skills:akka-best-practices`, `dotnet-skills:akka-hosting-actor-patterns`,
  `dotnet-skills:akka-testing-patterns`, `dotnet-skills:akka-net-specialist` (agent)
- Actor patterns: `dotnet-skills:csharp-concurrency-patterns`,
  `dotnet-skills:dotnet-concurrency-specialist` (agent)
- C# coding: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`
- Testing: `dotnet-skills:testcontainers` (Mosquitto), `dotnet-skills:snapshot-testing` (Verify)
- Project structure: `dotnet-skills:project-structure`, `dotnet-skills:package-management`
- DI / Config: `dotnet-skills:microsoft-extensions-dependency-injection`,
  `dotnet-skills:microsoft-extensions-configuration`
- Serialization: `dotnet-skills:serialization`
- OpenTelemetry: `dotnet-skills:opentelementry-dotnet-instrumentation`
