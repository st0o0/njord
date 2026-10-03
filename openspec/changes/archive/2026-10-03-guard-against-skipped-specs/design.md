## Context

SW001 skips `*Spec.cs`. The architecture test suite already runs on every test run, so
a test there closes the gap without tooling changes.

## Decisions

- **Plain reflection, not ArchUnitNET.** ArchUnit models attributes by type name but not
  their argument values, so `Skip = "..."` is invisible to it. Reflection with
  `CustomAttributeData` reads named arguments without instantiating attributes and also
  sees attributes on non-public methods and nested types.
- **Assemblies:** `Njord.Tests` (via the existing test type) and `Njord.Tests.Shared`
  (via `typeof(FakeOpenMeteoClient)`). Types come from `GetTypes()` (includes nested and
  non-public); methods use `Public | NonPublic | Instance | Static | DeclaredOnly`.
- **Detection:** any attribute whose type derives from `Xunit.FactAttribute` (covers
  Theory) with a non-empty named argument `Skip`, `SkipUnless` or `SkipWhen`; plus any
  attribute whose type name is `Ignore`/`IgnoreAttribute` (any namespace), on methods or
  classes. The scan is a pure helper tested directly against fixture types.
- **No permanent fixtures.** A permanent skipped fixture would itself violate the rule; red evidence comes from a temporary skipped test removed after the run.
- Plain `[Fact]` (synchronous, as in the other architecture specs).

## Risks

- Attribute argument values that are not literal strings (for example constants) still appear as typed values in `CustomAttributeData`; the check treats any non-null, non-empty value as skipped.
