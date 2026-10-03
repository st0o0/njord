## ADDED Requirements

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
