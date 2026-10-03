## MODIFIED Requirements

### Requirement: Actor and stream tests use deterministic assertions
All actor and stream tests SHALL use Akka TestKit's `TestProbe` with
`ExpectMsg<T>` for positive assertions and `ExpectNoMsg` for negative
assertions. Tests MUST NOT use polling-based `AsyncAssert.WaitUntil` or
`AsyncAssert.StaysTrue` for actor message assertions. Tests SHOULD NOT
use custom collector classes when TestProbe provides equivalent functionality.

#### Scenario: Positive assertion uses ExpectMsg
- **WHEN** a test asserts that an actor produced a message
- **THEN** it uses `TestProbe.ExpectMsg<T>()` instead of polling a shared collection

#### Scenario: Negative assertion uses ExpectNoMsg
- **WHEN** a test asserts that an actor did NOT produce a message within a period
- **THEN** it uses `TestProbe.ExpectNoMsg(duration)` instead of `AsyncAssert.StaysTrue`

#### Scenario: Stream events route to TestProbe
- **WHEN** a test needs to assert on messages flowing through an Akka Stream
- **THEN** it routes them to a TestProbe via `Sink.ForEach(m => probe.Tell(m))` and uses `ExpectMsg` for assertions

#### Scenario: Batch draining uses ReceiveWhile
- **WHEN** a test needs to wait for a batch of messages to finish before asserting on subsequent messages
- **THEN** it uses `TestProbe.ReceiveWhile<T>()` to drain the batch, then `ExpectMsg` for new messages

#### Scenario: Tests pass deterministically on CI
- **WHEN** all tests run on a shared GitHub Actions runner
- **THEN** zero tests fail due to timing or thread starvation
