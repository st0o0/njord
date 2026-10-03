## MODIFIED Requirements

### Requirement: Actor and stream tests use deterministic assertions
All actor and stream tests SHALL use Akka TestKit's `TestProbe` with
`ExpectMsg<T>` for positive assertions and `ExpectNoMsg` for negative
assertions. Tests MUST NOT use polling-based `AsyncAssert.WaitUntil` or
`AsyncAssert.StaysTrue` for actor message assertions.

#### Scenario: Positive assertion uses ExpectMsg
- **WHEN** a test asserts that an actor produced a message
- **THEN** it uses `TestProbe.ExpectMsg<T>()` instead of polling a shared collection

#### Scenario: Negative assertion uses ExpectNoMsg
- **WHEN** a test asserts that an actor did NOT produce a message within a period
- **THEN** it uses `TestProbe.ExpectNoMsg(duration)` instead of `AsyncAssert.StaysTrue`

#### Scenario: Tests pass deterministically on CI
- **WHEN** all 574 tests run on a shared GitHub Actions runner
- **THEN** zero tests fail due to timing or thread starvation
