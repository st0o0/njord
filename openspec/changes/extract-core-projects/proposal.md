## Why

njord is one 185-file production assembly. The three-zone rule (Ingest / Domain / Egress) and the dependency direction are enforced only by ArchUnit specs, and the type-level coupling shows real cycles (`Configuration` ↔ `Domain`, `Persistence` ↔ `Pipeline`, `Enrichment` ↔ `Mqtt`). FunkArr solves the same problem with a bottom hub (`Messages`, `Persistence`, `Core`) and domain libraries that reference only `Core`, so the compiler enforces direction. This is stage 1 of that split: build the shared bottom layer and break the cycles that block later extraction. No feature code is extracted yet.

Sibling changes (same target layout): `fix-test-warnings` (prerequisite cleanup), `extract-leaf-feature-projects` (stage 2), `extract-pipeline-egress-projects` (stage 3). Prerequisite: `akka-failure-hygiene` is applied (typed `XxxFailed` messages already exist and move as messages).

## What Changes

Target layout for all stages (stage 1 creates the first four):

```
Njord.Domain       pure model + the 6 option POCOs the model needs        refs: none
Njord.Persistence  DTOs only (extend-only), no mapping logic              refs: none
Njord.Messages     shared actor messages / contracts                      refs: Domain
Njord.Core         options, validators, budget helpers, Diagnostics,
                   NjordHealthState, StreamSupervision,
                   StreamConsumerActor, IOpenMeteoClient                  refs: Domain, Messages, Persistence
Njord (host)       everything else stays here in stage 1                  refs: Core (+ transitive)
-- stages 2/3: Ingest, Pipeline, Enrichment, Egress, Mqtt, Grpc, Sensors → each refs Core only
```

- Create `Njord.Domain` from `Domain/{Weather,Sensors,Analysis}` and move `AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride` and `LocationOptions` down into it. This breaks `Configuration` ↔ `Domain` and lets `WeightedTarget` live below `Messages`.
- Create `Njord.Persistence` with the five `*Dtos.cs` files. Mapping logic (`*Mapping` classes) moves out to the owning feature code (still in the host in stage 1), which removes `Persistence` ↔ `Pipeline` without moving `ModelPollState`.
- Create `Njord.Messages` from the cross-feature actor messages (egress bus, pipeline/scheduler/budget messages, sensor messages, snapshot messages, `Ack`). Messages nested or co-located in actor files are hoisted first.
- Create `Njord.Core` from `Configuration` (except the three `*Setup.cs` files, which stay in the host), `Diagnostics`, `NjordHealthState`, `StreamSupervision`, `StreamConsumerActor` and `IOpenMeteoClient`.
- Add FunkArr-style actor marker interfaces: `src/Njord.Core/ActorKeys.cs` (13 empty `IXxxActor` markers, e.g. `ISchedulerActor`, `IPipelineActor`, `IEgressActor`), register every actor under its marker in the host `NjordActorSystemSetup` (grouped per domain like FunkArr's `AkkaSetupContainer`) and replace all class-keyed lookups (`Get<SchedulerActor>()`, `GetActorAsync<PipelineActor>()`, ~70 sites incl. tests) by marker lookups. Not `IRequiredActor<T>`, not per-library `WithXActors()`. No new package.
- Wire solution, references, `InternalsVisibleTo`, ArchUnit assembly loading, `Dockerfile` project copies, `AGENTS.md` solution structure and the cited paths in the `njord-*` skills.
- **BREAKING**: moving the persistence DTOs to a new assembly changes their type identity, so journal/snapshot rows written by the current build may no longer be readable. njord is 0.x, so no compatibility shims or migration code are added (same stance as FunkArr); existing Docker-volume persistence data may need to be reset. `[JsonProperty]` names and `Version` values are untouched. The change is committed as `refactor!:` with a `BREAKING CHANGE:` footer.

## Capabilities

### New Capabilities

None. Pure structural refactor; `skip_specs: true`.

### Modified Capabilities

None.

## Impact

- Code: new `src/Njord.Domain/`, `src/Njord.Persistence/`, `src/Njord.Messages/`, `src/Njord.Core/`; `git mv` of files out of `src/Njord/`; `using` updates across `src/Njord/` and `src/Njord.Tests/`.
- Build: `src/Njord.slnx`, `src/Njord/Njord.csproj` (references), `Dockerfile` (per-project restore copy), `src/Njord.Tests/Architecture/*`.
- Docs: `AGENTS.md` (solution structure, guardrail wording), `.claude/skills/njord-*/SKILL.md` (paths).
- CI: workflows build `Njord.slnx` and need no change; `security.yml` already matches `src/**/*.csproj`.
- Runtime/API budget: no behavior change, 0 additional Open-Meteo requests/month (no polling is added or altered).

## Non-goals

- No behavior change; wire formats, topic names, entity set and gRPC contracts stay identical (Verify snapshots must not change).
- No extraction of feature code into libraries (stages 2 and 3).
- No Enrichment/Mqtt descriptor refactor; `IEnrichmentFeature`, `MqttMessage`, `TopicScheme` and the payload builders stay in the host.
- No change to CI design or workflows.
- No namespace overhaul for existing code beyond the policy in design.md.
