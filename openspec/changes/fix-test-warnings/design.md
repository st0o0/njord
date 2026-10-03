## Context

See proposal.md. Facts from the current test sources (verified by reading/grep, no build run because another change is editing production code):

- The five `*StateSpec` classes are pure synchronous tests (`public void`, no I/O) that carry `[Fact(Timeout = 5000)]` purely by convention. A synchronous test cannot be cancelled by a token, which is exactly what `xUnit1069` flags.
- `Architecture/ZoneArchitectureSpec.cs` and `ConventionArchitectureSpec.cs` are synchronous too; they use a 60 s timeout (`ArchitectureTimeoutMs`) because loading the ArchUnit model takes 6-10 s. ArchUnit's `.Check(...)` takes no token.
- `Grpc/WeatherGrpcServiceSpec.cs` lines 190 and 203 are async tests with `Timeout` that do not reference the token; they are new work from `akka-failure-hygiene`, so the real count after that change is 60, not 58.
- `src/.editorconfig` has the naming rules (`private_static_fields_must_be_pascal_case` at `warning`) but no `dotnet_diagnostic.*` entries.
- `AGENTS.md` and the project convention state "`[Fact(Timeout = 5000)]`" for all specs.

## Goals / Non-Goals

**Goals:** zero `xUnit1069`/`IDE1006` in the build; the rule becomes an error so it stays at zero; the documented convention matches what the tests do.

**Non-Goals:** see proposal.md.

## Decisions

### 1. Drop `Timeout` from synchronous tests; require the token in async tests that keep one

- Pure synchronous specs (state specs, architecture specs): remove `Timeout = ...`. A timeout there is not enforceable (nothing cooperates with cancellation) and pure functions cannot hang; the whole-run timeout of the test runner remains the backstop.
- Async tests keep `Timeout = 5000` and pass `TestContext.Current.CancellationToken` into their awaited calls (`Ask`, `ExpectMsgAsync`, `ThrowsAsync` delegates where an API accepts a token). Where an awaited API has no token overload, the test still references the token once through the closest API that takes one.
- `AGENTS.md` convention becomes: "`[Fact(Timeout = 5000)]` for async/actor tests, passing `TestContext.Current.CancellationToken`; pure synchronous specs omit the timeout."

*Alternatives:* (a) pass `TestContext.Current.CancellationToken` into nothing just to silence the analyzer, rejected as a lie that suppresses rather than fixes; (b) `#pragma`/`NoWarn xUnit1069`, rejected (the slopwatch skill exists to catch exactly that shortcut); (c) convert sync specs to `async Task` returning `Task.CompletedTask`, rejected as noise.

The two architecture specs lose their 60 s timeout comment too; the ArchUnit model load is already shared through a static field.

### 2. Rename, do not relax, the `IDE1006` fields

`_njordAssembly`, `_testsAssembly` (NjordArchitecture.cs) and `_testAttributes` (ConventionArchitectureSpec.cs) become `NjordAssembly`, `TestsAssembly`, `TestAttributes`. The naming rule is project policy (private static fields are PascalCase; instance fields `_camelCase`).

### 3. Severity: `xUnit1069` to `error`, `IDE1006` stays as configured

Add `dotnet_diagnostic.xUnit1069.severity = error` to `src/.editorconfig`. `IDE1006` is already surfaced as a warning via the naming-rule severity, so no extra entry is added; the build output after the fix is the check. Promoting it to an error is a separate analyzer change.

### 4. Count-agnostic task list

The task list is driven by the build output, not by the number 58: first re-run the build, collect every `xUnit1069` site, fix by file, rebuild until zero. This keeps the change valid whether or not `akka-failure-hygiene` has landed.

### 5. `!.` note in `AGENTS.md`

Re-grep `src/Njord.Tests` for `!.` (excluding the allowed `JsonNode` indexer form), replace "about 20" with the actual count and the real top files. Doc-only; fixing the `!.` sites is out of scope.

## Risks / Trade-offs

- [Dropping `Timeout` on sync specs removes a nominal safety net] -> It was never enforceable for synchronous code; the runner's session timeout still bounds a hang.
- [Making the rule an error breaks the build for a later author] -> That is the intent; the message names the rule and the fix.
- [`akka-failure-hygiene` adds more sites concurrently] -> Task 1 re-collects the list; if it is not yet applied, apply this change after it.
- [Convention text diverges from the zone-architecture-tests spec/tasks that mention the 60 s timeout] -> Update the mention in `openspec/changes/zone-architecture-tests/tasks.md` only if that change is still unarchived; otherwise leave history untouched.

## Migration Plan

Single commit; rollback by revert. No runtime impact.
