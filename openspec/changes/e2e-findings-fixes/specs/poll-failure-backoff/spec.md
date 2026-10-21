## ADDED Requirements

### Requirement: Failed polls back off exponentially
A location's domain poll that fails (transport error, HTTP error, no usable data) SHALL be retried after a delay that
doubles with each consecutive failure: 60 s after the first, 2 min after the second, 4 min after the third and so
on, capped at the configured poll interval. Rate-limit and budget rejections SHALL wait at least 5 min;
pipeline-unavailable rejections SHALL keep their 5 s retry and SHALL NOT count as a failure.

#### Scenario: First failure
- **WHEN** a domain that last succeeded fails once
- **THEN** the next poll of that domain SHALL be scheduled 60 s later

#### Scenario: Consecutive failures double the delay
- **WHEN** the same domain fails for the third time in a row
- **THEN** the next poll SHALL be scheduled 4 min later

#### Scenario: Delay is capped at the poll interval
- **WHEN** a domain has failed ten times in a row and the poll interval is 60 min
- **THEN** the next poll SHALL be scheduled 60 min later

#### Scenario: Rate limit keeps its floor
- **WHEN** the first failure of a domain is a rate-limit rejection
- **THEN** the next poll SHALL be scheduled no earlier than 5 min later

#### Scenario: Other domains unaffected
- **WHEN** Marine fails repeatedly for a location
- **THEN** the Weather poll schedule of that location SHALL be unchanged

### Requirement: Success resets the backoff
A successful poll SHALL reset the domain's consecutive-failure count, so the next failure is retried after 60 s again.

#### Scenario: Failure after recovery
- **WHEN** a domain fails three times, then succeeds, then fails once
- **THEN** the retry after the last failure SHALL be scheduled 60 s later

### Requirement: Backoff survives a restart
The consecutive-failure count SHALL be persisted with the poll outcome, so a recovered poll worker continues the
backoff instead of restarting at 60 s. Records written before this change SHALL recover with a count of zero.

#### Scenario: Recovery continues the backoff
- **WHEN** a poll worker recovers after a domain failed four times in a row and then fails again
- **THEN** the next poll of that domain SHALL be scheduled 16 min later

#### Scenario: Old records
- **WHEN** a poll worker recovers an outcome record without a failure count
- **THEN** the count SHALL be zero
