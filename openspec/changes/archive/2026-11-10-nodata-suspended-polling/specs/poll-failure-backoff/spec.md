## MODIFIED Requirements

### Requirement: Failed polls back off exponentially
A location's domain poll that fails (transport error, HTTP error) SHALL be retried after a delay that
doubles with each consecutive failure: 60 s after the first, 2 min after the second, 4 min after the third and so
on, capped at the configured poll interval. Rate-limit and budget rejections SHALL wait at least 5 min;
pipeline-unavailable rejections SHALL keep their 5 s retry and SHALL NOT count as a failure. NoData failures SHALL bypass exponential backoff entirely and instead use a fixed 24-hour delay as defined in the `nodata-domain-suspension` spec.

#### Scenario: First failure
- **WHEN** a domain that last succeeded fails once with a transport or model error
- **THEN** the next poll of that domain SHALL be scheduled 60 s later

#### Scenario: Consecutive failures double the delay
- **WHEN** the same domain fails for the third time in a row with a transport error
- **THEN** the next poll SHALL be scheduled 4 min later

#### Scenario: Delay is capped at the poll interval
- **WHEN** a domain has failed ten times in a row and the poll interval is 60 min
- **THEN** the next poll SHALL be scheduled 60 min later

#### Scenario: Rate limit keeps its floor
- **WHEN** the first failure of a domain is a rate-limit rejection
- **THEN** the next poll SHALL be scheduled no earlier than 5 min later

#### Scenario: Other domains unaffected
- **WHEN** Marine fails repeatedly for a location
- **THEN** the Forecast poll schedule of that location SHALL be unchanged

#### Scenario: NoData bypasses exponential backoff
- **WHEN** a domain receives a NoData failure
- **THEN** the domain SHALL NOT use exponential backoff
- **AND** the retry delay SHALL be 24 hours
- **AND** the consecutive-failure count SHALL NOT be incremented
