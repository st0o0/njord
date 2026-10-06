## Why

Previous restructuring changes (dissolve-njord-tests, align-test-depth-with-funkarr) left behind empty project directories, stub E2E specs with no real assertions, and redundant setup/wiring tests now covered by the E2E Docker stack. A FunkArr comparison confirmed explicit wiring tests are unnecessary when E2E covers real host boot, and that Tests.Shared should only contain genuinely multi-project infrastructure.

## What Changes

- **Delete empty project directories** (`src/Njord.Tests/`, `src/Njord.Integration.Tests/`, `src/Njord.E2E.Tests/`) — not in solution, not in git, pure filesystem leftovers
- **Delete E2E stub specs and fixture infrastructure** from `Njord.IntegrationTests` — 5 skipped stub specs (`SingleModelHappyPathSpec`, `MultiModelConsensusSpec`, `EnrichmentPipelineSpec`, `HaBirthRediscoverySpec`, `SensorPushIntegrationSpec`) plus `E2EFixture`, `MosquittoFixture`, `FakeOpenMeteoHandler`; remove unused packages (`Testcontainers`, `MQTTnet`, `Verify.XunitV3`)
- **Delete redundant setup/wiring tests** — `ActorKeyRegistrationSpec` (12 Theory cases), `NjordServiceSetupSpec` (6 Facts), `NjordActorSystemSetupSpec` (2 Facts); keep `PersistenceBeforeActorsSpec` (fail-fast guard) and `StreamShutdownTaskSpec` (unique shutdown coverage)
- **Move `PersistenceRoundtripHelper`** from `Njord.Tests.Shared` to `Njord.Persistence.Tests` (sole consumer)
- **Consolidate `FailingRefProvider` duplicate** — `Mqtt.Tests` has a variant with an extra `SubscribeInbound` handler; merge into the shared version

## Non-goals

- Changing production code
- Changing any passing test logic
- Extracting `Njord.Compute` (separate change)
- No API-budget impact — no polling changes

## Capabilities

### New Capabilities
- `test-cleanup`: Removal of dead test infrastructure, redundant wiring tests, and Tests.Shared hygiene

### Modified Capabilities
- `test-project-structure`: Updated to reflect removed specs, cleaned IntegrationTests, and consolidated Tests.Shared

## Impact

- **Deleted files**: 8 spec/infrastructure files from IntegrationTests (~284 lines), 3 empty directories
- **Deleted tests**: ~20 test methods (all redundant with E2E Docker stack)
- **Modified project**: `Njord.IntegrationTests.csproj` — 3 packages removed
- **Modified project**: `Njord.Tests.Shared` — 1 file moved out
- **Modified project**: `Njord.Persistence.Tests` — gains `PersistenceRoundtripHelper`
- **Modified project**: `Njord.Mqtt.Tests` — local `FailingRefProvider` deleted, uses shared version
- **Modified**: `AGENTS.md` — test counts updated
