## ADDED Requirements

### Requirement: Typed receives are followed by content assertions

A `Assert.NotNull` on a value whose type is already proven by
`ExpectMsg<T>()` or `Ask<T>()` is vacuous. The assertion following a
typed receive SHALL inspect actual content.

#### Scenario: Typed ask result
- **WHEN** `Ask<T>` returns a result whose type is already proven
- **THEN** the test asserts on a meaningful property of it (a request id, a reason, a value) instead of `Assert.NotNull`

## REMOVED Requirements

### Requirement: No vacuous assertions after typed receives
**Reason**: Its only scenario used the `Ack` reply of the PollScheduler, which no longer exists; the rule moves unchanged to "Typed receives are followed by content assertions" with a current example.
**Migration**: None.
