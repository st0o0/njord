## Context

Previous restructuring changes (`dissolve-njord-tests`, `align-test-depth-with-funkarr`) moved specs to their domain test projects and created an E2E fixture with Testcontainers, but left behind empty project directories, placeholder specs, and didn't clean up superseded wiring tests. A FunkArr comparison confirmed: explicit wiring tests are unnecessary when E2E covers the real host boot, and Tests.Shared should only contain genuinely multi-project infrastructure.

## Goals / Non-Goals

**Goals:**
- Remove all dead test infrastructure (empty dirs, stub specs, unused fixtures)
- Remove wiring tests made redundant by the E2E Docker stack
- Ensure Tests.Shared contains only genuinely shared helpers
- Keep tests with unique coverage (shutdown, fail-fast guard)

**Non-Goals:**
- Changing production code
- Changing passing test logic
- Extracting Njord.Compute (separate change)

## Decisions

### D1: Delete empty directories outright

**Decision**: `rm -rf` the three empty project directories (`Njord.Tests/`, `Njord.Integration.Tests/`, `Njord.E2E.Tests/`).

**Alternative**: Keep as placeholders. Rejected because they're not in the solution, not in git, and create confusion about project structure.

### D2: Delete all E2E stubs and their infrastructure

**Decision**: Remove all 5 E2E stub specs, the `E2EFixture`, `MosquittoFixture`, `FakeOpenMeteoHandler`, and the `Testcontainers`/`MQTTnet`/`Verify.XunitV3` package references from `Njord.IntegrationTests`.

**Alternative**: Flesh out the stubs into real tests. Rejected because the `e2e/` Docker stack skill is the right place for full E2E testing — in-process xUnit with Testcontainers duplicates that infrastructure at a different abstraction level.

### D3: Wiring test removal criteria

**Decision**: Remove a wiring test if **all** of these hold: (a) a broken wiring would prevent the E2E Docker stack from starting, (b) `dotnet run` would also fail, (c) the test doesn't exercise a unique behavior (like coordinated shutdown). Keep tests where the E2E stack wouldn't catch the failure.

Applied:
- **Remove**: `ActorKeyRegistrationSpec` (E2E boots real actors), `NjordServiceSetupSpec` (E2E uses real DI), `NjordActorSystemSetupSpec` (trivial reflection)
- **Keep**: `StreamShutdownTaskSpec` (unique — shutdown isn't tested by E2E), `PersistenceBeforeActorsSpec` (fail-fast invariant, cheap to keep)

### D4: Add SubscribeInbound handler to shared FailingRefProvider

**Decision**: Add `Receive<SubscribeInbound>(_ => { })` to the shared `FailingRefProvider` in `Tests.Shared`, then delete the Mqtt.Tests local copy. This is the only difference between the two versions.

**Alternative**: Keep both versions. Rejected because the extra handler is harmless in non-MQTT contexts (the message simply won't arrive).

### D5: Move PersistenceRoundtripHelper to its sole consumer

**Decision**: Move `PersistenceRoundtripHelper.cs` from `Njord.Tests.Shared` to `Njord.Persistence.Tests` and update its namespace. It's only used by `Persistence.Tests`.

## Risks / Trade-offs

- **[Losing early wiring feedback]** → Wiring bugs will surface later (at E2E time) instead of at unit-test time. Acceptable because (a) `dotnet run` catches them immediately, (b) the E2E skill runs in CI.
- **[SubscribeInbound handler in shared fake]** → Non-MQTT test projects gain an unused message handler. Negligible — the handler is a no-op and the message type is already in the shared dependency chain via `Njord.Core`.

## Open Questions

None — all decisions are straightforward deletions or moves.
