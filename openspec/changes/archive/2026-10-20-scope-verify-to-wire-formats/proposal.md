## Why

Aligning with FunkArr's principle: Verify snapshot testing is used surgically for wire-format contracts only (persistence and gRPC), not broadly. Currently 7 Njord test projects reference Verify but only 2 use it for wire formats. 3 projects have unused Verify dependencies, and 2 use it for non-wire-format purposes (options defaults, MQTT payloads) where explicit assertions are clearer.

## What Changes

- **Remove unused Verify**: Remove Verify.XunitV3 package reference and ModuleInitializer.cs from Core.Tests, Pipeline.Tests, Budget.Tests (zero actual Verify calls).
- **DefaultOptionsSpec**: Remove the Verify golden master test. Keep the validator-passes and override-wins tests (these test real behavior, not a snapshot).
- **Mqtt StatePayloadGoldenMasterSpec**: Replace 6 Verify snapshot tests with explicit JSON assertion tests. Remove the 6 .verified.txt files. Remove Verify package reference and ModuleInitializer.cs from Mqtt.Tests.
- **Keep**: Persistence.Tests (2 wire-format snapshots) and Grpc.Tests (7 proto-mapping golden masters) — these pin actual wire contracts.

## Non-goals

- No changes to production code.
- No changes to Persistence.Tests or Grpc.Tests Verify usage.
- No changes to shared test infrastructure (NjordActorSpec, TestTimeouts, AppSettingsDefaults).

## Capabilities

### New Capabilities

- `verify-scoping`: Rules for when to use Verify vs explicit assertions in Njord tests.

### Modified Capabilities

_(none)_

## Impact

- **Test projects**: Core.Tests, Pipeline.Tests, Budget.Tests (remove unused), Mqtt.Tests (replace snapshots), Njord.Tests (remove one snapshot)
- **Package references**: Verify.XunitV3 removed from 5 of 7 projects
- **Net test change**: ~7 Verify snapshot tests replaced by ~7 explicit assertion tests (count preserved)
