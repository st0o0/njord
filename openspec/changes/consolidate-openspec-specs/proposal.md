## Why

The OpenSpec main specs directory has grown to 155 specs through incremental changes. Several groups now overlap significantly: 5 message-naming specs cover the same conventions, 7 persistence specs repeat record/testing rules, and 2 test-assertion-hygiene specs are near-duplicates. Two specs describe work that is fully implemented and now just restate CLAUDE.md/AGENTS.md conventions. Consolidating reduces noise when reading specs for context and prevents contradictory rules across overlapping specs.

## What Changes

- **Merge message-naming family (5→1)**: `message-naming`, `message-naming-consistency`, `message-naming-convention`, `message-response-naming` → merge unique requirements into `message-conventions` (already the most comprehensive). Delete the 4 redundant specs.
- **Merge persistence family (7→3)**: `persistence-records` + `persistence-record-hygiene` + `persistence-funkarr-alignment` + `persistence-roundtrip-tests` → merge into `persistence-records`. Rename `persistence-backoff` → `persistence-supervision`. Keep `persistence-provider` as-is. Delete `persistence-alignment` (content already in AGENTS.md).
- **Merge test-assertion-hygiene (2→1)**: Fold unique rules from `test-assertion-hygiene-2` into `test-assertion-hygiene`. Delete `-2`.
- **Delete stale specs**: `test-cleanup` (all target directories removed long ago), `xml-doc-cleanup` (zero `<summary>` tags remain in production code).
- **Keep**: `enrichment-model-envelope` (aspirational, not yet implemented).

## Non-goals

- No changes to archived changes.
- No changes to production code or tests.
- No changes to CLAUDE.md/AGENTS.md (specs feed into those, not the other way).
- Not touching the 10 e2e-* specs (intentionally granular per test section).
- Not auditing health-check or logging spec overlap (separate change if needed).

## Capabilities

### New Capabilities

- `spec-consolidation`: Rules for merging overlapping OpenSpec main specs without losing requirements.

### Modified Capabilities

_(none — this changes spec files, not system behavior)_

## Impact

- **openspec/specs/**: 11 spec directories affected (5 message, 7 persistence, 2 test-hygiene, 2 stale)
- **Net reduction**: ~155 specs → ~144 specs (-11)
- **No code changes**
