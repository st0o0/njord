## Context

After the gateway-pipeline-redesign refactoring, a full project review surfaced
17 findings. The codebase builds clean (0 warnings), all 655 tests pass, and
runtime behavior is correct. The findings are convention drift, architectural
test gaps, and post-refactoring debris.

The `njord-persistent-actor` skill was stale (documented pre-FunkArr conventions
including a mandatory Version field). It has been deleted and AGENTS.md updated
to reference the FunkArr `akka-skills:persistence` conventions directly.

## Goals / Non-Goals

**Goals:**
- Align persistence records with FunkArr naming and layout conventions
- Close the Pipeline gap in zone architecture tests
- Fix metric naming to follow the `njord_<area>_<name>` convention consistently
- Bring test assertions up to convention standard
- Fill test coverage gaps in Indices sensor-override logic
- Remove post-refactoring debris

**Non-Goals:**
- No runtime behavior changes
- No wire-format breaking changes (all `[JsonProperty]` strings stay identical)
- No new features or polling changes

## Decisions

### D1: Persistence record renames are a `refactor!:` commit

The project is 0.x, and AGENTS.md explicitly allows renaming persistence record
types as a breaking change during pre-1.0. Wire format is preserved because
`[JsonProperty]` strings are unchanged — only C# type names move. Existing
snapshots in the Akka journal deserialize by `[JsonProperty]` strings, not by
CLR type name (Newtonsoft's default behavior with `[JsonProperty]`).

Renames:
- `BudgetTrackerSnapshotDto` → `PersistedBudgetTrackerState`
- `ApiCallRecordedDto` → `ApiCallRecorded`
- `DataChangedDto` → `DataChanged`

### D2: Pipeline gets a dedicated zone test, not inclusion in the generic list

`ZoneArchitectureSpec` uses `FeatureLibraryNames` for a blanket
"no feature lib depends on any other" check. Pipeline can't join that list
because it legitimately imports the four analysis libraries. Instead, add a
dedicated test method that asserts Pipeline depends on no feature library
*except* Alerts, Derived, Trends, Indices.

### D3: Metric renames are safe — no Prometheus history concern

Njord is pre-release. No production Grafana dashboards exist referencing the old
metric names. Renaming is a straight edit with no migration needed.

### D4: Flaky test identification strategy

Run `Njord.Core.Tests` in a loop (5 iterations) to reproduce the failure. If it
reproduces, inspect the failing test for timing sensitivity (missing
`AwaitAssert`, tight timeouts, actor name reuse after `GracefulStop`). If it
doesn't reproduce, document it as non-reproducible and move on.

### D5: `!.` cleanup is best-effort on straightforward cases

The 15 known `!.` uses span multiple files. Fix the ones where the pattern is
simple (add `Assert.NotNull`, use the returned non-null variable). Skip cases
that would require restructuring test logic (e.g. field-level `!.` in test
fixtures). Document remaining count in AGENTS.md.

## Risks / Trade-offs

- **[Risk] Persistence renames break existing journal data** → Mitigated:
  `[JsonProperty]` strings are the wire contract, not CLR type names.
  Newtonsoft deserializes by property name. Verify wire-format snapshot tests
  will guard this going forward.
- **[Risk] Metric renames break dashboards** → Mitigated: no production
  dashboards exist yet (pre-release). Dev dashboards are easy to update.
- **[Risk] Flaky test not reproducible** → Accept: document it, move on.
  The single observation may have been environmental.
