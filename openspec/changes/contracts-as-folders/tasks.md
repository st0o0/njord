## 1. Spec reconciliation

- [ ] 1.1 After `analysis-contracts-per-feature` is archived, write MODIFIED/REMOVED deltas for the main specs it changed (`vertical-feature-libraries`, `architecture-zone-enforcement`, `compute-library`) so none names a `Njord.<Feature>.Contracts` project; `openspec validate contracts-as-folders` passes

## 2. Move contracts into Messages

- [ ] 2.1 Derived: move result records, enums and settings into `Njord.Messages/Derived` (namespace `Njord.Messages.Derived`), update usings, build green
- [ ] 2.2 Trends: same into `Njord.Messages/Trends`
- [ ] 2.3 Alerts: same into `Njord.Messages/Alerts`
- [ ] 2.4 Indices: same into `Njord.Messages/Indices`
- [ ] 2.5 Decide consensus result records (stay in `Njord.Consensus` or move to `Messages/Consensus`) and record the decision

## 3. Remove the contracts projects

- [ ] 3.1 Delete the four `Njord.<Feature>.Contracts` projects from `Njord.slnx`, project references, `Dockerfile` and CI; move their specs to `Njord.Messages.Tests` and drop the contracts test projects

## 4. Rules and docs

- [ ] 4.1 Update `LayerReferenceSpec`; add the Messages guard rule (no `*Analyzer`, `*Rule`, `*Formulas`, no Akka) with a spec
- [ ] 4.2 Update `AGENTS.md` (solution structure, reference direction, test totals) and docs

## 5. Verification

- [ ] 5.1 `dotnet format whitespace`, build `Njord.slnx` with 0 warnings, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
