## Why

The most important architecture guardrail in `CLAUDE.md` — Ingest, Domain and Egress are three zones that only meet in the domain model, and Ingest and Egress never reference each other — is enforced by nothing but convention. All of `Njord` is one assembly, so the compiler cannot protect it, and agents adding code will drift. Today the rule holds (see design.md); a test keeps it that way.

## What Changes

- Add architecture tests in `src/Njord.Tests/Architecture/` that load the `Njord` assembly with ArchUnitNET and assert the zone rules.
- Add `TngTech.ArchUnitNET.xUnitV3` to central package management (via `dotnet add package`) and reference it from `Njord.Tests`.
- Assert: `Njord.Ingest` does not depend on `Njord.Egress`/`Njord.Mqtt`/`Njord.Grpc`; Egress-side namespaces do not depend on `Njord.Ingest`; `Njord.Domain.*` depends on neither Ingest nor Egress nor transport namespaces.
- Cheap convention rules in the same spec: production classes are `sealed` (abstract excepted), test classes are `sealed` and `Spec`-suffixed.

## Capabilities

### New Capabilities

- `architecture-zone-enforcement`: architecture tests that fail the build when zone dependency rules or the sealed/Spec conventions are violated.

### Modified Capabilities

None.

## Impact

- Files: new `src/Njord.Tests/Architecture/*Spec.cs`, `src/Directory.Packages.props` and `src/Njord.Tests/Njord.Tests.csproj` (package added via `dotnet add package`). No production code changes.
- Dependency: one new test-only package (`TngTech.ArchUnitNET.xUnitV3`, same package FunkArr uses).
- API budget: 0 additional requests/month; no polling is added or altered.

## Non-goals

- Enforcing rules on Akka conventions (`Status.Failure`, `ContinueWith`); that is `akka-failure-hygiene`.
- Splitting `Njord` into multiple assemblies/projects.
- Enforcing acyclic dependencies between all namespaces (`Njord.Configuration` is cross-cutting and already references most zones).
- Naming rules for actors/messages beyond sealed/Spec.
- Running mutation or coverage gates.
