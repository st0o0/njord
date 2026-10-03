# CLAUDE.md

@AGENTS.md

## Workflow

Changes go through OpenSpec: `/opsx:explore` to think → `/opsx:propose` to create
a change (proposal/design/specs/tasks) → `/opsx:apply` to implement → `/opsx:archive`.
Implementation is TDD; run `dotnet slopwatch` (local tool) after substantial code
changes.

## Skill routing (invoke by name)

Prefer retrieval-led reasoning: consult these before implementing.

### Project skills (njord-specific)

- `njord-persistent-actor` -- **Load before creating/modifying a persistent actor
  or a persistence DTO.** Snapshot-only vs event+snapshot, recovery, cleanup,
  DTO/mapping shape.
- `njord-enrichment-feature` -- **Load before adding/changing an enrichment
  feature.** Interface choice, options toggle, registration, discovery + state
  payloads.
- `njord-actor-spec` -- **Load before writing actor, feature or persistence-DTO
  tests.** TestKit, in-memory persistence, `FakeTimeProvider`, Verify.

Requires the `akka-skills` plugin, which is not installed on every machine; routes below do not resolve without it.

### Akka.NET skills (akka-skills plugin)

- Actor state pattern: `akka-skills:actor-state` (state records, Apply/GetSnapshot, persistence)
- Persistence separation: `akka-skills:persistence` (three-tier state model, SaveSnapshot, extend-only DTOs)
- Project structure: `akka-skills:project-structure` (solution layout, domain isolation, ArchUnitNET)
- Message conventions: `akka-skills:messages` (VerbNoun commands, QueryNoun queries) — this project uses Pattern B (co-located with actors)
- Actor testing: `akka-skills:testing` (Classic + Hosting TestKit, async assertions, persistence testing)
- Logging: `akka-skills:logging` (ILoggingAdapter in actors, ILogger in services, Serilog setup)
- Setup containers: `akka-skills:setup-container` (Servus AppBuilder, DI/Actor/App composition)
- Cluster hosting: `akka-skills:cluster-hosting` (Singletons, ShardRegions, MessageExtractor)
- Actor pools: `akka-skills:actor-pools` (Router-Pools, DI-Pools, Stash-Capacity)
- Persistence setup: `akka-skills:persistence-setup` (WithSqlPersistence, Provider, Clustering)
- Become state machines: `akka-skills:become-state-machines` (multi-phase workflows, stash-during-init, connection lifecycle)
- Advanced patterns: `akka-skills:advanced-patterns` (IWithTimers, ReceiveAsync, PipeTo, DeathWatch, Passivation, PersistAll)
- Streams: `akka-skills:streams` (Source/Flow/Sink, MergeHub/BroadcastHub, StreamRefs, custom GraphStage, supervision)
- Supervision: `akka-skills:supervision` (BackoffSupervisor, custom SupervisorStrategy, escalation)

### Plugin skills

- Actors & supervision: `dotnet-skills:akka-best-practices`,
  `dotnet-skills:akka-hosting-actor-patterns`
- Messages & pipeline design: `dotnet-skills:csharp-concurrency-patterns`
- Domain modeling: `dotnet-skills:csharp-coding-standards`,
  `dotnet-skills:csharp-type-design-performance`
- Config & DI: `dotnet-skills:microsoft-extensions-configuration`,
  `dotnet-skills:microsoft-extensions-dependency-injection`
- Serialization (discovery/state payloads): `dotnet-skills:serialization`
- Testing: `dotnet-skills:akka-testing-patterns`,
  `dotnet-skills:testcontainers` (Mosquitto integration tests),
  `dotnet-skills:snapshot-testing` (discovery payloads via Verify)
- Packages & structure: `dotnet-skills:package-management`,
  `dotnet-skills:project-structure`
- Quality gates: `dotnet-skills:slopwatch` (after code changes)
- Specialist agents: `dotnet-skills:akka-net-specialist`,
  `dotnet-skills:dotnet-concurrency-specialist`
