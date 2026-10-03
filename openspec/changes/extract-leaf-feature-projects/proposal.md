## Why

Stage 2 of the FunkArr-style split of the single `Njord` assembly. After `extract-core-projects` (stage 1: `Njord.Domain`, `Njord.Persistence`, `Njord.Messages`, `Njord.Core`) the three leaf features can leave the host first, because nothing else depends on their classes: `Grpc` (11 files + protos), `Ingest` (5 files) and `Sensors` (1 file). This is the cheapest place to prove the project layout, per-project DI/Akka registration and per-assembly ArchUnit rules before the entangled features (Pipeline, Egress, Enrichment, Mqtt) move in stage 3.

## What Changes

- New projects `src/Njord.Grpc/`, `src/Njord.Ingest/`, `src/Njord.Sensors/`, each referencing only `Njord.Core` (never each other, never the host).
- Move `src/Njord/Grpc/*` (incl. its own snapshot actors/states, `GrpcSnapshotConsumerActor`, `SnapshotMessages`) and `protos/njord/v2/*.proto` compilation (`<Protobuf ... GrpcServices="Server">`) into `Njord.Grpc`; move `src/Njord/Ingest/*` into `Njord.Ingest`; move `src/Njord/Sensors/SensorHubActor.cs` into `Njord.Sensors`.
- Cross-library actor access uses the FunkArr-style `IXxxActor` marker interfaces that stage 1 creates in `src/Njord.Core/ActorKeys.cs` (no `IRequiredActor<T>`, no class keys); `Njord.Grpc` resolves `ISchedulerActor`, `IBudgetTrackerActor`, `IEgressActor`, `ISensorHubActor` and never references the other feature libs (see design).
- Actor registration stays central in the host `NjordActorSystemSetup` (FunkArr `AkkaSetupContainer` style, per-domain methods); the moved actor classes become `public`. The libs expose only `AddNjordGrpc()` / `AddNjordIngest()` (services) and `MapNjordGrpc(WebApplication)`; the host setup files call them instead of naming feature types.
- Host `Njord.csproj` gains three `ProjectReference`s and loses `Grpc.AspNetCore` and the `<Protobuf>` item.
- Per-assembly ArchUnit rules; `InternalsVisibleTo Njord.Tests` for each new project.
- Update `AGENTS.md` solution-structure section and the paths cited in the `njord-*` skills.
- Plan only (executed as tasks, no behavior change): Dockerfile restore/COPY lines for the new csprojs.

## Capabilities

### New Capabilities

None. Pure refactor; `skip_specs: true`.

### Modified Capabilities

None.

## Impact

- Prerequisite: `extract-core-projects` applied (Core owns `IOpenMeteoClient`, options, shared messages, the actor marker interfaces (`ActorKeys.cs`), `NjordHealthState`, `NjordMetrics`; persistence DTO mapping lives in the owning domain project).
- Code: `src/Njord/{Grpc,Ingest,Sensors}`, `src/Njord/Configuration/Njord{Service,ActorSystem,Application}Setup.cs`, `src/Njord/Njord.csproj`, `src/Njord.Tests/{Architecture,Grpc,Ingest,Sensors}`, `src/Njord.Tests.Shared`, `src/Njord.slnx`, `Dockerfile`, `AGENTS.md`, `.claude/skills/njord-*`.
- API budget: none; no polling added or altered (0 requests/month against the 300k free-tier limit).
- Wire/persistence formats unchanged (persistence IDs, DTO `[JsonProperty]` names, proto package `njord.v2`, actor names `scheduler`, `budget-tracker`, `forecast-snapshot`, ... stay identical).

## Non-goals

- Extracting `Pipeline`, `Egress`, `Enrichment`, `Mqtt` (stage 3) or creating `Core/Domain/Messages/Persistence` (stage 1).
- Any behavior change, rename of actors/persistence IDs/metrics/proto messages.
- Splitting the test project into per-library test projects (decision recorded in design.md; revisit after stage 3).
- Analyzer/`BannedSymbols.txt` rollout and CI changes.
