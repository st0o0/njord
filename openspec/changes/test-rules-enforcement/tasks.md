## 1. Scanner

- [ ] 1.1 Add `TestSourceScanner` (comment and literal stripping, attribute and signature parsing) with self-tests on synthetic snippets

## 2. Rules

- [ ] 2.1 Rule: async `[Fact]`/`[Theory]` has a `Timeout`
- [ ] 2.2 Rules: no `Thread.Sleep`, no literal `Task.Delay`, no wall-clock `UtcNow`/`Now` in specs
- [ ] 2.3 Rule: no empty `new XOptions()` for options with shipped defaults (type set derived from `DefaultOptionsSpec`)
- [ ] 2.4 Rules: no `Assert.True`/`Assert.False` around equality, no `!.` outside `JsonNode` receivers
- [ ] 2.5 Add allow-lists with reasons for remaining legitimate cases; stale entries fail

## 3. Documentation checks

- [ ] 3.1 Add the doc-reference spec for `AGENTS.md` and `.claude/skills/*/SKILL.md`
- [ ] 3.2 Add the test-count check and document the counting rule
- [ ] 3.3 Label every test convention in `AGENTS.md` as machine-checked (with the spec name) or review-only

## 4. Verification

- [ ] 4.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
