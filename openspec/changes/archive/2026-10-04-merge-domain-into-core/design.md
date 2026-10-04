## Context

Njord.Domain sits at the bottom of the dependency graph — referenced by
Messages, Core, and transitively by every feature library. It contains pure
types (Weather, Sensors), computation (Analysis), and options records. FunkArr
keeps all of this in Core directly. The merge is mechanical: move files, update
namespaces, adjust references.

## Goals / Non-Goals

**Goals:**
- Eliminate Njord.Domain as a separate project
- Preserve all types, computations, and tests
- Keep the folder structure readable inside Core

**Non-Goals:**
- Restructuring Analysis into feature libraries (rejected: result types are
  consumed across domains, violating lateral rules)
- Changing any computation logic

## Decisions

### Namespace convention: drop `Domain` segment, not add `Core`

Core files already use `Njord.{Folder}` namespaces (e.g., `Njord.Actors`,
`Njord.Configuration`), not `Njord.Core.{Folder}`. Domain files follow suit:
`Njord.Domain.Weather` → `Njord.Weather`, not `Njord.Core.Weather`.

**Alternative considered:** `Njord.Core.Weather`. Rejected — inconsistent with
every existing Core namespace.

### Options files merge into Configuration/

Domain's `Options/` folder already uses `namespace Njord.Configuration`. The
files move into `Njord.Core/Configuration/` alongside existing options/validators.
No namespace change needed.

### Newtonsoft.Json moves to Core

Domain's only PackageReference is Newtonsoft.Json (for `[JsonProperty]` on
~24 types). Core gains this dependency. This is acceptable — Core already
transitively depends on Newtonsoft via Persistence, and the `[JsonProperty]`
attributes are needed for serialization stability of domain types that appear
in persistence snapshots.

### Messages references Core instead of Domain

Currently: Messages → Domain, Core → Domain.
After: Messages → Core (Domain gone).

This changes Messages from referencing only the bottom layer to referencing
Core. That is a slight layer broadening, but since Core already depends on
Messages (Core → Messages → Core would be circular), we need to verify this
is actually safe.

**Wait — Core references Messages.** If Messages also references Core, that's
circular. So Messages must NOT reference Core. Instead, the types Messages
needs (WeatherModel, ParameterDef, etc.) are now in Core, so Messages would
need Core, creating a cycle.

**Resolution:** The types Messages needs are pure data records with no
dependencies on Core infrastructure. They can stay in a shared namespace
(`Njord.Weather`, `Njord.Sensors`) inside Core's assembly without Messages
needing to reference Core — **unless Messages literally has a
ProjectReference to Core's csproj.**

Since Messages currently references Domain (a zero-dependency project), and
after the merge those types live in Core (which references Messages), we have
a **circular dependency**. This must be resolved.

**Solution:** Keep a thin `Njord.Domain` package that holds ONLY the pure
data types that Messages needs (Weather records, Sensor records). Or:
move the shared types into Messages itself (Messages already exists at the
bottom).

**Better solution:** Move the types that Messages needs into Messages. These
are: `WeatherModel`, `CycleId`, `FetchOutcome`, `ParameterDef`, `ModelForecast`
(used in EgressEvent), `SensorKind`, `SensorReading`, `SensorSnapshot` and the
`ForecastSeries`/`ForecastPoint` types. Everything else (Analysis, Options,
ParameterRegistry, etc.) goes to Core.

Actually, looking more carefully at what Messages imports:
- `Njord.Domain.Weather` — `WeatherModel`, `CycleId`, `FetchOutcome`,
  `ModelForecast`, `ParameterDef`, `ForecastSeries` (EgressEvent carries these)
- `Njord.Domain.Sensors` — `SensorSnapshot`, `SensorReading`, `SensorKind`

These are the pure record types, no computation, no framework dependencies.

**Final decision:** Split Domain into two targets:
1. **Weather/, Sensors/ pure records** → stay in a thin bottom-layer project,
   renamed from `Njord.Domain` to keep it minimal. Or move into Messages.
2. **Analysis/, Options/** → merge into Core.

The cleanest FunkArr-like approach: keep `Njord.Domain` but strip it to only
the shared value types (Weather records, Sensor records). Everything else
(Analysis, Options) moves to Core. This eliminates the circular dependency
while reducing Domain to a thin shared-types project.

### Revised plan

```
Before:                          After:
Njord.Domain (67 files)          Njord.Domain (17 files — Weather + Sensors only)
  Weather/   (14)     ────────►    Weather/   (14)
  Sensors/   (3)      ────────►    Sensors/   (3)
  Analysis/  (46)     ────────►  Njord.Core/Analysis/  (46)
  Options/   (6)      ────────►  Njord.Core/Configuration/  (6)
```

Reference direction stays acyclic:
```
Domain (pure records) ← Messages ← Core ← feature libs ← host
                                    ↑
                              Analysis + Options now here
```

## Risks / Trade-offs

- [Low] Core gets 52 more files (Analysis 46 + Options 6) → Acceptable; Core
  is the natural home for shared computation
- [Low] Domain.Tests splits — Analysis tests move to Core.Tests, Weather/Sensor
  tests stay in Domain.Tests → Clean split along the same boundary
- [None] No circular dependencies — Domain stays pure, Messages → Domain stays

## Open Questions

(none — the circular dependency risk surfaced and was resolved during design)
