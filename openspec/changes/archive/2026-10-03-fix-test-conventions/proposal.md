## Why

A code quality review against FunkArr standards found 50+ test convention
violations across the Njord test suite. Most are in recently extracted test
projects (Grpc.Tests) where conventions were not carried over. Fixing these
aligns test code quality with the documented conventions in CLAUDE.md and
AGENTS.md.

## What Changes

- Add `[Fact(Timeout = ...)]` to 28 async test methods missing explicit timeouts
- Replace 5 `Assert.Single(x); x[0]` patterns with `var item = Assert.Single(x)`
- Add count guards before ~10 unguarded index accesses
- Replace 4 `!.` null-forgiving usages with `Assert.NotNull()` + clean access
- Pass `CancellationToken` through 5 async test methods in NjordServiceSetupSpec
- Remove 28 XML doc comments from 8 production and test files
- Convert 2 public fields to properties in test fakes

## Capabilities

### New Capabilities

- `test-assertion-hygiene`: Fix assertion patterns (Assert.Single capture,
  count guards, null-forgiving operator) and missing timeouts/CancellationToken
  across all test projects.
- `xml-doc-cleanup`: Remove XML doc comments that violate the "no XML docs"
  convention.

### Modified Capabilities

(none)

## Non-goals

- Fixing the 21 known legacy `!.` usages documented in CLAUDE.md — those are
  tracked separately.
- Changing test structure or adding new test coverage.
- Modifying any production behavior.

## Impact

- **Test files changed:** ~15 spec files across Grpc.Tests, Egress.Tests,
  Pipeline.Tests, Core.Tests, Domain.Tests, and Njord.Tests.
- **Production files changed:** 5 files (XML doc removal only in Core, Domain,
  Mqtt — no behavioral change).
- **API budget:** Zero — no polling or HTTP changes.
- **Risk:** Minimal — all changes are in test code or cosmetic production
  changes (comment removal). No behavioral impact.
