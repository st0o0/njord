## Context

The energy enrichment feature is one of seven enrichment features in njord's pipeline. It computes HeatingDemand, CopEstimate, CopOptimalHours, ShadingScore, BatteryStrategy, and NightCoolingPotential from consensus forecast data plus static configuration values (IndoorTemp, FlowTemp, CarnotEfficiency, HeatingBaseTemp).

Energy is wired into every layer: domain logic, enrichment pipeline, gRPC (proto messages, mapper, admin mutations, weather queries), MQTT egress (discovery + state payloads), persistence DTOs, configuration/validation, and ~20 OpenSpec specs that reference it in lists.

Njord is unreleased — no deployed consumers depend on the energy gRPC messages or MQTT entities.

## Goals / Non-Goals

**Goals:**
- Completely remove all energy enrichment code, configuration, proto definitions, tests, and documentation
- Leave the remaining six enrichments (consensus, alerts, derived, trends, indices, history) fully functional and unchanged
- Keep the build green and all non-energy tests passing after each task

**Non-Goals:**
- No replacement feature — energy management moves out of njord entirely
- No migration/tombstone code — njord is unreleased, no backwards compatibility needed
- No changes to the enrichment pipeline architecture itself (interfaces, actor topology stay as-is)
- CHANGELOG historical entries are left untouched

## Decisions

### 1. Bottom-up deletion order

Remove in dependency order: domain → enrichment feature → egress → gRPC → config → tests → docs. This avoids intermediate compile errors from dangling references. Each task should leave the build green.

**Alternative considered:** Top-down (config first, then gRPC, then domain). Rejected because removing config first would break DI resolution for EnergyEnrichment before its code is gone.

### 2. Proto field removal via `reserved`

When removing proto fields (`EnergyConfig energy = 6`, `EnergyUpdate energy = 5`, etc.), mark the field numbers and names as `reserved` to prevent accidental reuse in the future. Delete the standalone messages (`EnergyUpdate`, `EnergyConfig`, `CopOptimalHour`) entirely.

**Alternative considered:** Just delete the fields without reserving. Rejected because proto best practice is to reserve removed field numbers, even for unreleased APIs — it's cheap insurance.

### 3. OpenSpec specs: surgical edits only

The ~20 shared specs that mention energy in feature lists (e.g. "consensus, alerts, derived, trends, indices, energy, history") get minimal edits — remove "energy" from enumerations and examples. No structural changes to those specs.

### 4. Envelope spec removal

The `enrichment-model-envelope` spec exists primarily for energy envelope fields (`HeatingDemandMax`, `CopEstimateMin`, `CopOptimalConservative`). If no other enrichment uses the envelope pattern after energy is removed, delete this spec entirely. If other enrichments reference it, edit to remove energy-specific content only.

## Risks / Trade-offs

**Broad edit surface** — ~50 files touched, mostly small edits. Risk of missing a reference.
→ Mitigation: `dotnet build` after source changes; grep for `Energy`, `energy`, `cop`, `heating_demand`, `shading`, `battery_strategy`, `night_cooling` before marking complete.

**Verified snapshot breakage** — Persistence serialization tests use Verify snapshots that include energy. Removing the energy type mapping will break the verified `.txt` file.
→ Mitigation: Re-verify the snapshot after removing the energy entry from `EnrichmentSnapshotDtos`.

**Proto regeneration** — Removing messages/fields from `.proto` files requires regenerating C# code. If the project uses `Grpc.Tools` auto-generation, the build handles this. If manual, the generated files need updating.
→ Mitigation: Verify proto codegen approach before editing protos.
