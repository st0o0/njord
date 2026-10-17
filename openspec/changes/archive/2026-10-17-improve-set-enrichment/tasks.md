## 1. Writer batch API (TDD)

- [x] 1.1 `src/Njord.Core.Tests`: failing specs for `IConfigWriter.UpdateManyAsync` on a real temp override file (all valid → one write and one reload; one invalid → nothing written; reset removes override keys; reset then update)
- [x] 1.2 `src/Njord.Core/Configuration/IConfigWriter.cs` + `ConfigWriter.cs`: `UpdateManyAsync` with section updates and reset, one `PersistAsync`, one `Reload`; key removal path in the override merge
- [x] 1.3 Keep `Njord.Architecture.Tests` green (writer stays in `Njord.Core`)

## 2. ConfigManager and messages

- [x] 2.1 `src/Njord.Messages/Config/`: extend `UpdateAnalysisConfig` with `Reset` (section set) and `DryRun`; rejected response carries issues (section + message) using a domain-side section enum; one file per message
- [x] 2.2 `src/Njord.Tests` (ConfigManager specs): empty command rejected, multi-section atomicity, dry run persists nothing, reset applied before update, restart-required list unchanged for toggles
- [x] 2.3 `src/Njord/Configuration/ConfigManager.cs`: `UpdateAnalysisConfigAsync` uses `UpdateManyAsync`; dry run returns after validation; collect issues per section

## 3. Proto and mapping

- [x] 3.1 `protos/njord/v2/admin.proto`: enum `EnrichmentSection`, message `ConfigIssue`, `SetEnrichmentRequest.reset = 7` / `dry_run = 8` (6 stays reserved), `ConfigResponse.issues = 6` / `restart_required = 7` / `dry_run = 8`
- [x] 3.2 `src/Njord.Grpc.Tests`: failing mapping/service specs (issues, restart_required not in warnings, dry-run response shape, UNSPECIFIED reset reported, empty request)
- [x] 3.3 `src/Njord.Grpc/Mapping/ConfigProtoMappings.cs`: map request (reset, dry_run), `Applied`/`Rejected` fill `restart_required` and `issues` while `rejection_reason` keeps the joined text
- [x] 3.4 Update golden masters in `Njord.Grpc.Tests` and review the diff

## 4. Docs and specs

- [x] 4.1 `docs/`: SetEnrichment reference (reset, dry run, issues, restart_required) and the changed `warnings` semantics for all mutation RPCs
- [x] 4.2 Note the `ConfigResponse` change in `AGENTS.md` only if the gRPC section describes it

## 5. Validation

- [x] 5.1 `dotnet build Njord.slnx`, the sequential test loop from `AGENTS.md`
- [x] 5.2 `dotnet format` whitespace check and slopwatch
- [x] 5.3 `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh`
- [ ] 5.4 Apply after `typed-wire-enums`; the last commit of the release carries the single footer `Release-As: 0.5.0` (once, not on every commit)
