## 1. README

- [x] 1.1 Rewrite `README.md` — describe njord as Open-Meteo → MQTT bridge for HA, include docker-compose usage, configurable locations/models, and build/test commands. Remove all Kachelmann references.

## 2. Git LFS removal

- [x] 2.1 Replace `.gitattributes` content with `* text=auto` only (remove all `filter=lfs diff=lfs merge=lfs` lines).

## 3. Gitignore cleanup

- [x] 3.1 Rewrite `.gitignore` to ~30 lines covering: .NET build output (`[Bb]in/`, `[Oo]bj/`, `[Ll]og/`), IDE files (`.vs/`, `.idea/`, `*.user`, `*.suo`, `*.DotSettings.user`, `*.sln.iml`), NuGet (`*.nupkg`), test results, MSBuild binary log (`*.binlog`), environment files (`**.env`), and project-specific custom entries (`coverage-results/`, `results/`, `.worktrees/`, `.claude/settings.local.json`, `*.received.*`, `openspec/changes/archive/`).

## 4. OpenSpec archive removal

- [x] 4.1 `git rm -r openspec/changes/archive/` to untrack the 206 archived change files.
- [x] 4.2 Verify `openspec/changes/archive/` is included in the new `.gitignore` (handled in task 3.1).

## 5. Dockerignore cleanup

- [x] 5.1 Remove the `docs` line from `.dockerignore`.

## 6. Slopwatch example removal

- [x] 6.1 `git rm .slopwatch/config.json.example` to remove the misleading protobuf/gRPC suppression example.

## 7. Validation

- [x] 7.1 Run `dotnet build Njord.slnx` from `src/` to confirm no build impact.
- [x] 7.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` to confirm all tests pass.
- [x] 7.3 Verify `git ls-files openspec/changes/archive/` returns no files.
- [x] 7.4 Verify `git lfs ls-files` returns no files (no LFS objects in history).
