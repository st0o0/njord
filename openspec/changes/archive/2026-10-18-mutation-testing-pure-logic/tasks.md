## 1. Spike

- [ ] 1.1 Install Stryker.NET 5.x as a local tool and run it with the MTP runner and `perTest` coverage on `Njord.Derived`; confirm killed and survived mutants are reported and note the run time (stop and report if it does not work)

## 2. Configuration

- [ ] 2.1 Pin the tool in `.config/dotnet-tools.json`; add `src/stryker-config.json` (runner, coverage analysis, reporters, thresholds, common exclusions) and git-ignore `StrykerOutput/`
- [ ] 2.2 Add per-project configs for Weather, Consensus (or Compute), Alerts, Derived, Trends, Indices and the Core `mutate` globs
- [ ] 2.3 Add `scripts/run-mutation.sh <project>` and document the command in `AGENTS.md` and `docs/development/mutation-testing.md`

## 3. CI

- [ ] 3.1 Add `.github/workflows/mutation.yml` (weekly and manual, matrix per project, report artifacts, score in job summary, not a required check)

## 4. Baseline and improvements

- [ ] 4.1 Run every configured project once, record the mutation score per project in the docs
- [ ] 4.2 Close surviving mutants in the alert rules and consensus statistics with exact-value assertions; list justified exclusions with reasons
- [ ] 4.3 Re-run those two projects and record the new scores; decide per project whether to set a `break` threshold (documented, not enforced in PRs)

## 5. Verification

- [ ] 5.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
