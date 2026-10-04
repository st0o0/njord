## Why

FunkArr has no separate Domain project — domain types live in Core. Njord.Domain
is an extra layer with 67 files, but only 17 of them (Weather + Sensors records)
MUST stay at the bottom because Messages references them. The other 50 files
(Analysis computations + Options records) belong in Core where they are consumed.

Moving Analysis and Options into Core reduces Domain to a thin shared-types
project and puts computation next to the infrastructure that drives it.

## What Changes

- Move `Analysis/` (46 files) from `Njord.Domain` → `Njord.Core/Analysis/`
- Move `Options/` (6 files) from `Njord.Domain` → `Njord.Core/Configuration/`
  (already use `Njord.Configuration` namespace — no namespace change needed)
- Update namespaces: `Njord.Domain.Analysis` → `Njord.Analysis`
- Move corresponding tests: Analysis specs (14 files) from `Njord.Domain.Tests`
  → `Njord.Core.Tests/Analysis/`
- `Njord.Domain` retains `Weather/` (14 files) and `Sensors/` (3 files) — the
  pure records that Messages depends on (avoids circular Core ↔ Messages ref)
- `Njord.Domain.Tests` retains Weather and Sensors specs
- Add Newtonsoft.Json to Core (Analysis types use `[JsonProperty]`)
- Update architecture tests and AGENTS.md

## Capabilities

### New Capabilities

- `domain-core-merge`: Move Analysis and Options from Njord.Domain into
  Njord.Core, reducing Domain to shared value types only.

### Modified Capabilities

(none)

## Non-goals

- Eliminating Njord.Domain entirely — impossible without circular dependencies
  (Messages → Domain, Core → Messages).
- Renaming Njord.Domain to something else.
- Changing any computation logic or domain behavior.
- Moving Weather/Sensors types — they must stay in Domain for Messages.

## Impact

- **Files moved:** 52 production files (46 Analysis + 6 Options), 14 test files
- **Projects modified:** Njord.Core (gains files + Newtonsoft.Json),
  Njord.Core.Tests (gains Analysis tests), Njord.Domain (loses Analysis + Options),
  Njord.Domain.Tests (loses Analysis tests), architecture tests, AGENTS.md
- **Namespace changes:** `Njord.Domain.Analysis` → `Njord.Analysis` across ~30 files
- **API budget:** Zero.
- **Risk:** Medium — large file move but purely mechanical. `refactor!:` commit.
