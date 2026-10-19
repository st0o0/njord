## 1. Delete stale specs

- [x] 1.1 Delete `openspec/specs/test-cleanup/` (all target directories removed long ago)
- [x] 1.2 Delete `openspec/specs/xml-doc-cleanup/` (zero `<summary>` tags remain in production code)

## 2. Merge message-naming family (5→1)

- [x] 2.1 Read all 5 specs: `message-naming`, `message-naming-consistency`, `message-naming-convention`, `message-conventions`, `message-response-naming`. Extract unique requirements from each.
- [x] 2.2 Merge unique requirements into `openspec/specs/message-conventions/spec.md` — deduplicated, kept most precise wording. Preserved: query response naming (drop Query prefix), command response naming (VerbNoun pattern), handshake failure naming (Request*Failed), Failed carries exception, domain namespace organization.
- [x] 2.3 Delete `openspec/specs/message-naming/`, `openspec/specs/message-naming-consistency/`, `openspec/specs/message-naming-convention/`, `openspec/specs/message-response-naming/`

## 3. Merge persistence family (7→3)

- [x] 3.1 Read all 7 specs. Extracted unique requirements from each.
- [x] 3.2 Merge `persistence-records` + `persistence-record-hygiene` + `persistence-funkarr-alignment` + `persistence-roundtrip-tests` + snapshot/cleanup rules from `persistence-alignment` into `openspec/specs/persistence-records/spec.md`. Preserved all: positional records, FunkArr naming, DateTimeOffset/TimeSpan, self-contained types, enum conversions, no dead versioning, file-per-type layout, mappers outside state, Apply takes events, snapshot scheduling, journal cleanup, roundtrip tests, wire-format tests.
- [x] 3.3 Rename `openspec/specs/persistence-backoff/` → `openspec/specs/persistence-supervision/`
- [x] 3.4 Delete `openspec/specs/persistence-alignment/`, `openspec/specs/persistence-record-hygiene/`, `openspec/specs/persistence-funkarr-alignment/`, `openspec/specs/persistence-roundtrip-tests/`
- [x] 3.5 Keep `openspec/specs/persistence-provider/` unchanged

## 4. Merge test-assertion-hygiene (2→1)

- [x] 4.1 Read both. Unique rules in `-2`: no Assert.True for equality, typed receives followed by content assertions.
- [x] 4.2 Merged unique rules from `-2` into `openspec/specs/test-assertion-hygiene/spec.md`
- [x] 4.3 Delete `openspec/specs/test-assertion-hygiene-2/`

## 5. Validation

- [x] 5.1 `openspec validate --all --no-interactive` — 144 passed, 3 failed (pre-existing validation issues in forecast-value-type, model-forecast-split, solution-zone-structure — unrelated to this change)
- [x] 5.2 Spec count: 144 (was ~155, -11). Target met.
