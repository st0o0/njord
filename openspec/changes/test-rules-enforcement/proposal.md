## Why

The test conventions in `AGENTS.md` (explicit timeouts on async facts, no sleeps or arbitrary delays, no wall-clock data, `AppSettingsDefaults` instead of `new XOptions()`, `Assert.NotNull` instead of `!.`, `Assert.Equal` instead of `Assert.True(a == b)`) are enforced today only by review, apart from three rules that code checks: sealed `Spec` classes, no disabled tests and the timeout margin of `TestTimeoutsSpec` (plus the `xUnit1069` compiler error). The survey found 7 `Task.Delay` calls, 71 default-reliant options constructions and wall-clock test data that slipped through. FunkArr documents the same conventions and also leaves them unenforced; njord already scans production code for rules (`EnumConversionArchitectureSpec`, `MappingConventionArchitectureSpec`) and can apply the same technique to test sources.

## What Changes

- New `TestSourceConventionArchitectureSpec` scans the `*Spec.cs` files of all test projects (source text with a small token-aware scanner, like the existing enum rule) and fails on:
  - an `async Task` `[Fact]`/`[Theory]` without a `Timeout =` argument, unless the project's tests are pure synchronous;
  - `Thread.Sleep`, and `Task.Delay` with a literal duration (an allow-list for the two cancellation-bound infinite delays);
  - `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `DateTime.Now` in spec files;
  - `new <Name>Options()` with an empty initializer for options that have a shipped default (use `AppSettingsDefaults`);
  - `Assert.True(<a> == <b>)`/`Assert.False(<a> != <b>)` comparisons that should use `Assert.Equal`/`Assert.NotEqual`;
  - `!.` on non-`JsonNode` receivers (the known deviations are an explicit, shrinking allow-list).
- Each rule has negative self-tests (a synthetic offending snippet is detected) and an allow-list whose stale entries fail the spec, like the existing architecture specs.
- A doc-reference spec checks that helper and file names mentioned in `AGENTS.md` and `.claude/skills/*/SKILL.md` exist (symbols in `Njord.Tests.Shared`, spec file names), and that the documented total test count matches the sum reported by the test projects (computed from the assemblies via xUnit discovery, not from running them).
- `AGENTS.md` documents which conventions are machine-checked and which remain review rules.

## Capabilities

### New Capabilities
- `test-conventions`: machine-checked test source conventions and documentation references.

### Modified Capabilities
<!-- none -->

## Impact

- New spec files in `Njord.Architecture.Tests` (a test-source scanner helper plus one spec); allow-list entries for current violations that `test-gaps-and-drift` has not yet fixed; `AGENTS.md`.
- No production change. Not breaking; commit type `test:`.
- Applies after `test-gaps-and-drift` so the allow-lists start nearly empty.
