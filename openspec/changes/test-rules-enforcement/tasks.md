## 1. Scanner

- [x] 1.1 Add `TestSourceScanner` (comment and literal stripping, attribute and signature parsing) with self-tests on synthetic snippets

## 2. Rules

- [x] 2.1 Rule: async `[Fact]`/`[Theory]` has a `Timeout`
- [x] 2.2 Rules: no `Thread.Sleep`, no literal `Task.Delay`, no wall-clock `UtcNow`/`Now` in specs
- [x] 2.3 Rule: no empty `new XOptions()` for options with shipped defaults (type set derived from `DefaultOptionsSpec`)
- [x] 2.4 Rules: no `Assert.True`/`Assert.False` around equality, no `!.` outside `JsonNode` receivers
- [x] 2.5 Add allow-lists with reasons for remaining legitimate cases; stale entries fail

## 3. Documentation checks

- [x] 3.1 Add the doc-reference spec for `AGENTS.md` and `.claude/skills/*/SKILL.md`
- [x] 3.2 Add the test-count check and document the counting rule
- [x] 3.3 Label every test convention in `AGENTS.md` as machine-checked (with the spec name) or review-only

## 4. Verification

- [x] 4.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
