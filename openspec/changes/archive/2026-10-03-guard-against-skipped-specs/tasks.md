## 1. Red

- [x] 1.1 Add the architecture spec and a temporary skipped dummy test; run the new spec and confirm it fails naming the dummy (failed: "Disabled tests found: Njord.Tests.TempDummySkipSpec.Dummy")
- [x] 1.2 Remove the dummy test

## 2. Green

- [x] 2.1 Run the new spec and the full suite; expect green
- [x] 2.2 Update the slopwatch known-limits wording in `AGENTS.md` and `CLAUDE.md`

## 3. Validation

- [x] 3.1 `dotnet build Njord.slnx` has 0 errors and 0 warnings; `dotnet format Njord.slnx whitespace --verify-no-changes` is clean
- [x] 3.2 `dotnet slopwatch analyze -d . --fail-on warning` passes; no `*.verified.*` changes
- [x] 3.3 `openspec validate --all --no-interactive` passes
