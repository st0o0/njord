## Purpose

Defines assertion and test-method hygiene rules for the test projects: explicit timeouts, no boolean equality assertions, captured Assert.Single values, count guards, Assert.NotNull over null-forgiving, no vacuous assertions after typed receives, cancellation tokens, and properties on fakes.

## Requirements

### Requirement: Async test methods SHALL have explicit timeouts

Every async test method (`async Task`) SHALL carry `[Fact(Timeout = ...)]` or
`[Theory(Timeout = ...)]`. TestKit-based tests that make dilated Akka waits
SHALL use `TestTimeouts.Hosted`; plain async tests SHALL use `Timeout = 5000`.

#### Scenario: TestKit-based async test has timeout
- **WHEN** a test class extends `TestKit` and contains an `async Task` test method
- **THEN** the method has `[Fact(Timeout = TestTimeouts.Hosted)]`

#### Scenario: Plain async test has timeout
- **WHEN** a test method is `async Task` but does not use TestKit waits
- **THEN** the method has `[Fact(Timeout = 5000)]`

### Requirement: No Assert.True for equality checks

Test code SHALL use `Assert.Equal(expected, actual)` or `Assert.Empty()`
instead of `Assert.True(x == y)` or `Assert.True(collection.Count == 0)`.

#### Scenario: Equality check uses Assert.Equal
- **WHEN** asserting two values are equal
- **THEN** the test SHALL use `Assert.Equal(a, b)` instead of `Assert.True(a == b)`

#### Scenario: Empty check uses Assert.Empty
- **WHEN** asserting a collection is empty
- **THEN** the test SHALL use `Assert.Empty(collection)` instead of `Assert.True(collection.Count == 0, ...)`

### Requirement: Assert.Single return value SHALL be captured

When asserting a single-element collection, the return value of `Assert.Single`
SHALL be captured and used instead of indexing the original collection.

#### Scenario: Single-element access uses Assert.Single capture
- **WHEN** a test asserts that a collection has exactly one element and accesses it
- **THEN** the code reads `var item = Assert.Single(collection)` followed by assertions on `item`, not `collection[0]`

### Requirement: Collection indexing SHALL have a count guard

When accessing elements by index, the test SHALL first assert the expected count
with `Assert.Equal(N, collection.Count)`.

#### Scenario: Multi-element access is guarded by count assertion
- **WHEN** a test accesses `collection[0]` and `collection[1]`
- **THEN** `Assert.Equal(2, collection.Count)` (or equivalent) precedes the index access

### Requirement: Nullable results SHALL use Assert.NotNull instead of null-forgiving

Test code SHALL NOT use the null-forgiving operator (`!.`) on nullable results.
It SHALL use `Assert.NotNull(value)` before accessing members.

#### Scenario: Nullable property access uses Assert.NotNull
- **WHEN** a test accesses a nullable property (e.g., `request.RequestUri`)
- **THEN** the code reads `Assert.NotNull(request.RequestUri)` before accessing `.ToString()`

### Requirement: Typed receives are followed by content assertions

A `Assert.NotNull` on a value whose type is already proven by
`ExpectMsg<T>()` or `Ask<T>()` is vacuous. The assertion following a
typed receive SHALL inspect actual content (a property value, a count, a reason).

#### Scenario: Typed ask result needs content assertion
- **WHEN** `Ask<T>` returns a result whose type is already proven
- **THEN** the test asserts on a meaningful property (a request id, a reason, a value) instead of just `Assert.NotNull`

### Requirement: Async tests SHALL pass CancellationToken

Async test methods SHALL pass `TestContext.Current.CancellationToken` to awaited
calls that accept a cancellation token.

#### Scenario: Async test passes cancellation token
- **WHEN** an async test method awaits a call that accepts `CancellationToken`
- **THEN** it passes `TestContext.Current.CancellationToken`

### Requirement: Test fake fields SHALL be properties

Public members on test fakes and nested test classes SHALL be auto-properties,
not fields.

#### Scenario: Test fake uses property instead of field
- **WHEN** a nested test fake class exposes a mutable counter
- **THEN** it uses `public int Count { get; set; }` not `public int Count;`
