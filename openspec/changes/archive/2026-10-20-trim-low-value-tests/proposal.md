## Why

Comparison with FunkArr (the reference project) shows ~8-9% of Njord's 1753 tests are low-value: they verify compiler-guaranteed record behavior, duplicate coverage already provided by completeness checks, or test third-party library internals. These tests add maintenance cost on every enum/record change without catching real bugs. FunkArr achieves ~0% waste with the same wire-format safety by relying on completeness tests and golden masters instead of per-member pinning.

## What Changes

- **Messages.Tests**: Remove per-member `[InlineData]` wire-name rows where a `cover_every_*_member` completeness test already exists (~100 tests). The completeness tests stay — wire coverage is preserved.
- **Model.Tests**: Remove tests that verify compiler-generated record property accessors and equality (~20 tests). Tests for custom logic (LocationId case-insensitive equality, parsing, registry) stay.
- **SensorHub.Tests**: Remove `SensorHubStateSpec` tests that verify basic `ImmutableDictionary` behavior (~6 tests). Tests with real logic (aggregation, case-insensitive lookup, expiry filtering) stay.
- **TelemetrySpec consolidation**: Replace per-instrument name assertions in Core, Pipeline, Mqtt, OpenMeteo with a single Verify golden master per project (~12 tests become 4 snapshot tests).
- **Grpc.Tests**: Remove per-member `[InlineData]` rows in `WireEnumMappingsSpec` where `Every_domain_member_has_a_proto_mapping` completeness test covers it. Remove `TimestampConversionSpec` (tests Google.Protobuf, not project code) (~41 tests).

## Non-goals

- Analysis feature tests (Alerts, Derived, Trends, Indices) — already clean.
- Consensus, Pipeline actor tests, Mqtt payload/topic tests, Persistence, Host, Architecture, Budget — all valuable.
- No production code changes.
- No reduction in wire-format coverage — completeness tests and golden masters remain.

## Capabilities

### New Capabilities

- `test-trimming`: Rules and patterns for identifying and removing low-value tests across the Njord test suite.

### Modified Capabilities

_(none — no spec-level behavior changes)_

## Impact

- **Test projects**: Messages.Tests, Model.Tests, SensorHub.Tests, Core.Tests, Pipeline.Tests, Mqtt.Tests, OpenMeteo.Tests, Grpc.Tests
- **CLAUDE.md**: Test count must be updated after cleanup
- **No production code affected**
- **No API or wire-format changes**
- **Net reduction**: ~140-160 tests removed, 4 Verify snapshots added
