# TODO — week of 2026-10-26

Each item becomes its own OpenSpec change (`/opsx:explore` → `/opsx:propose`).

## ~~1. Rewrite the OpenMeteoGateway~~ DONE

Stream-only with `Source.Queue` + `SelectAsyncUnordered`, shard-routed replies. Archived as
`rewrite-gateway` (commits `d44c10fc`..`6ac8f185`).

## 2. Replace `WeatherResponseConverter` with clean API models — PARTIALLY DONE

Typed response DTOs exist for all six domains. Remaining:
- `WeatherResponseConverter` (112 LOC) still handles ensemble-key splitting (`temperature_2m_<model>`)
  and member-series skipping via a custom `JsonConverter`. Remove it and handle multi-model responses
  in the DTO or a clean mapping step.

## ~~3. Clean up requirement and model resolution in `DomainSetupContainer`~~ DONE

`EffectiveWeatherModels`, `ServiceInfo`, `ResolvedRequirements` and the raw `IReadOnlySet<DomainKind>`
singleton are gone. `FeatureRequirementsOptions` is configured via `OptionsBuilder` in
`DomainSetupContainer` (commit `bb373bd2`).

## ~~4. Clean client design and a structured `Njord.OpenMeteo` project~~ DONE

Domain folders (`Weather/`, `AirQuality/`, `Flood/`, `Marine/`, `Seasonal/`, `Satellite/`) with
client + API enums + DTOs + mappings. Infrastructure in `Gateway/`, `Budget/`, `Http/`,
`Configuration/`. Shared helpers in `Mapping/` (commit `c29e3c7e`).

## 5. Remove the magic strings in `Njord.OpenMeteo` — PARTIALLY DONE

Infrastructure is in place: `[JsonStringEnumMemberName]` on all six `*ApiVariable` enums,
`ApiVariableNames` with `FrozenDictionary` lookup (commit `e453b9af`). Remaining:
- ~27 hard-coded literals in `WeatherResponseConverter` (`BaseNames`/`ApiNames` FrozenSets)
- Scattered literals in client files (query keys, fixed values like `"hourly"`, `"unixtime"`)
- Endpoint paths in clients (could become constants on `OpenMeteoEndpoints`)
- Overlaps with item 2: killing the converter removes most of the remaining literals.

## 6. Design a clean options / config system

- Rethink the whole options and configuration model from scratch, so every consumer gets what it needs
  directly from one well-defined source.
- Goal: no derived helper types or factory registrations. `FeatureRequirementsOptions` is the one
  derived type now; decide if that shape is final or needs refinement.
- To decide in explore: which values are operator input (locations, feature toggles, domain options)
  and which are derived (domains, params, models, call weights), where the derived values are computed
  once and how they are exposed.

## 7. Look at `FeatureDescriptor`

- `Name` is used for more than the config section: `FeatureResolution` matches it against
  `ComparisonFeatureName`, `OpsGrpcService` builds `Category:Name` strings.
- `PollDepth` sizes the ring in `FeatureEngine`/`FeatureRunner`. Currently only 3 features use it
  (ConfidenceTrend, PollenTrend, ApproachingFront at depth 1).
- Decide if `Name` should stay a string or become something typed.

## 8. Rewrite all tests in the FunkArr style

- Order: after items 2 and 5, so the OpenMeteo tests are not rewritten twice.
- Decided:
  - Suffix `Tests` instead of `Spec` (classes and files).
  - Replace `NjordActorSpec` and the spec base classes completely.
  - Integration tests work with probes, as in FunkArr.
  - Layout 1:1 as in FunkArr, including the `XxxStateTests` pairs.
  - Naming as in FunkArr.
  - Test data, the shared project and data-only specs on the FunkArr level.
  - No `Timeout`: FunkArr sets none; `TestTimeouts`, the timefactor and the dilation rules go.
- Adapt the AGENTS.md rule "each enum conversion has a spec pinning every member" to the FunkArr level.
- Take over the FunkArr code and test conventions (FunkArr `AGENTS.md` and memories) where they fit
  njord; AGENTS.md and `ArchitectureSpec` follow.
- AGENTS.md already describes the target conventions (updated 2026-10-21); the test code,
  `Njord.Tests.Shared` and the `njord-actor-spec` skill (to become `njord-test`) still follow the old
  ones.
