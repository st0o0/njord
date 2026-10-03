## 1. Package and scaffolding

- [ ] 1.1 From `src/`: `dotnet add Njord.Tests/Njord.Tests.csproj package TngTech.ArchUnitNET.xUnitV3` (central package management; do not edit versions in the csproj); confirm the version lands only in `src/Directory.Packages.props`
- [ ] 1.2 Create `src/Njord.Tests/Architecture/NjordArchitecture.cs`: static class exposing the loaded `Architecture` (from the `Njord` assembly via `typeof(Njord.Ingest.OpenMeteoClient).Assembly`) and layer providers for Ingest, Domain, Egress side (`Njord.Egress`, `Njord.Mqtt`, `Njord.Grpc`)

## 2. Zone rules (red first)

- [ ] 2.1 Create `src/Njord.Tests/Architecture/ZoneArchitectureSpec.cs` (sealed): `Ingest_does_not_depend_on_egress_side`, `Egress_side_does_not_depend_on_ingest`, `Domain_does_not_depend_on_ingest_or_egress_side`
- [ ] 2.2 Prove each rule can fail: temporarily add a `using`+reference from `Njord/Ingest/` to `Njord.Egress`, from `Njord/Egress/` to `Njord.Ingest`, and from `Njord/Domain/Weather/` to `Njord.Mqtt`; run, see the failure name the offender; revert each probe (do not commit probes)
- [ ] 2.3 Run against the clean tree; all three pass (design.md Decision 1: zero current violations)

## 3. Convention rules

- [ ] 3.1 Create `src/Njord.Tests/Architecture/ConventionArchitectureSpec.cs` (sealed): `Production_classes_are_sealed` (non-abstract classes in `Njord`), `Test_classes_with_tests_are_sealed_and_suffixed_Spec` (types in `Njord.Tests` declaring `Fact`/`Theory` methods; exclude generated entry point and helpers)
- [ ] 3.2 Prove each rule can fail with a temporary unsealed class / misnamed test class; revert
- [ ] 3.3 Run against the clean tree; both pass

## 4. Timing and docs

- [ ] 4.1 Measure the spec run time; if `[Fact(Timeout = 5000)]` is too tight for architecture loading, use a documented larger timeout on these specs only
- [ ] 4.2 Add one line to `AGENTS.md` (or `CLAUDE.md` if `restructure-agent-docs` is not applied yet) under architecture guardrails: zone rules are enforced by `src/Njord.Tests/Architecture/`

## 5. Validation

- [ ] 5.1 `cd src && dotnet build Njord.slnx`
- [ ] 5.2 `cd src && dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Architecture.ZoneArchitectureSpec"`
- [ ] 5.3 `cd src && dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Architecture.ConventionArchitectureSpec"`
- [ ] 5.4 `cd src && dotnet run --project Njord.Tests/Njord.Tests.csproj` (full suite stays green)
- [ ] 5.5 `dotnet slopwatch` from the repo root reports nothing new
- [ ] 5.6 `openspec validate zone-architecture-tests`; commit with a Conventional Commit (`test: enforce zone architecture rules`); do not push
