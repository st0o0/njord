## Purpose

Defines further assertion hygiene rules for tests: no Assert.True for equality, no vacuous assertions after typed receives, captured Assert.Single values and Assert.NotNull over null-forgiving.
## Requirements
### Requirement: No Assert.True for equality checks

Test code SHALL use `Assert.Equal(expected, actual)` or `Assert.Empty()`
instead of `Assert.True(x == y)` or `Assert.True(collection.Count == 0)`.

#### Scenario: DisabledTestArchitectureSpec offender check
- **WHEN** asserting an offender list is empty
- **THEN** the test SHALL use `Assert.Empty(offenders)` instead of `Assert.True(offenders.Count == 0, ...)`

#### Scenario: CycleId equality
- **WHEN** asserting two CycleIds are equal
- **THEN** the test SHALL use `Assert.Equal(a, b)` instead of `Assert.True(a == b)`

### Requirement: Assert.Single captures the value

When `Assert.Single` is used, the test SHALL capture the return value
instead of indexing `[0]` on the collection afterward.

#### Scenario: BudgetCalculator warning capture
- **WHEN** asserting a single warning exists
- **THEN** the test SHALL use `var warning = Assert.Single(result.Warnings)` instead of `Assert.Single(...)` then `[0]`

#### Scenario: StatePayloadBuilder message capture
- **WHEN** asserting a single message exists
- **THEN** the test SHALL use `var message = Assert.Single(messages)` instead of `Assert.Single(...)` then `[0]`

### Requirement: Prefer Assert.NotNull over null-forgiving on nullable results

New test code SHALL use `Assert.NotNull(value)` before accessing nullable
members instead of the null-forgiving operator (`!.`). Existing `!.` uses
SHALL be cleaned up where straightforward.

#### Scenario: Nullable result member access
- **WHEN** a test accesses a member on a nullable result
- **THEN** the test SHALL call `Assert.NotNull(result)` first, then access the member on the asserted-non-null value

### Requirement: Typed receives are followed by content assertions

A `Assert.NotNull` on a value whose type is already proven by
`ExpectMsg<T>()` or `Ask<T>()` is vacuous. The assertion following a
typed receive SHALL inspect actual content.

#### Scenario: Typed ask result
- **WHEN** `Ask<T>` returns a result whose type is already proven
- **THEN** the test asserts on a meaningful property of it (a request id, a reason, a value) instead of `Assert.NotNull`

