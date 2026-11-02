## Why

`NjordHost` is a thin static class that only chains 12 `.Setup()` calls and `ClearProviders()`. It exists solely so `Program.cs` and `NjordTestHost` share the same composition. But the integration tests already intentionally diverge (fake clients, test server, different config), and `HostSmokeTests` validates the full composition against real endpoints. Inlining the setup chain into both call sites removes an indirection layer that adds no safety and forces all changes through a shared chokepoint.

## What Changes

- Inline the setup chain from `NjordHost.Configure(builder)` into `Program.cs` (~18 lines).
- Inline the same chain into `NjordTestHost.StartAsync`, with the existing service overrides between configure and build.
- Inline `NjordHost.Configure(app)` (single `app.Setup(new NjordApplicationSetup())`) into both call sites.
- Delete `NjordHost.cs`.
- Update `NjordTestHost` class comment (no longer references `NjordHost.Configure`).

## Non-goals

- Changing the setup order or adding/removing setups.
- Changing the test host's service overrides or fake clients.
- Removing `NjordApplicationSetup` or any `XxxSetup` class.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `terp-bootstrap`: The `NjordHost.Configure` split requirement is removed; `Program.cs` owns its setup chain directly, and the test host builds its own.

## Impact

- **Code**: `NjordHost.cs` deleted, `Program.cs` grows from 6 to ~18 lines, `NjordTestHost.cs` replaces 2 `NjordHost.Configure` calls with inline setup chains.
- **Tests**: `HostSmokeTests` continues to validate the full composition. No test behavior changes.
- **APIs**: No changes to gRPC, HTTP, or configuration.
- **Docs**: `AGENTS.md` line referencing `NjordHost` updated.
