# analysis-rules Specification

## Purpose
Makes every alert and every activity/utility index an independent, stateless unit that
can be tested and changed on its own, while the combined analysis output keeps a fixed,
predictable order.
## Requirements
### Requirement: Each alert type is evaluated by its own rule
Each alert type SHALL be evaluated by exactly one stateless rule that takes the consensus
location consensus and the current alert settings and returns one alert of that type. A rule SHALL NOT
depend on another rule's output.

#### Scenario: One rule per alert type
- **WHEN** the registered alert rules are listed
- **THEN** there is exactly one rule for each of the 14 alert types

#### Scenario: Rule tested in isolation
- **WHEN** the frost rule is called with a location consensus whose minimum temperature in
  the next 24 h is -3 °C, with full model agreement, and frost thresholds (0, -2, -5)
- **THEN** it returns a frost alert with the severity that the thresholds classify -3 °C
  into and expected low -3 °C, without any other rule running

### Requirement: Each index kind is scored by its own score
Each index kind SHALL be computed by exactly one stateless score that takes the day-slice
means and the resolved preferences for its kind and returns the score with its confidence
envelope.

#### Scenario: One score per index kind
- **WHEN** the registered index scores are listed
- **THEN** there is exactly one score for each of the 8 index kinds

### Requirement: Analysis output order is fixed
The alerts in an alert result SHALL be ordered by alert type declaration order, and the
scores in a day's index result SHALL be ordered by index kind declaration order,
independent of rule registration order.

#### Scenario: Registration order does not leak
- **WHEN** the alert rules are registered in reverse order
- **THEN** the produced alert result lists the alerts in the same order as before

### Requirement: Settings apply per evaluation
Rules and scores SHALL read thresholds and preferences only from the settings passed to the
evaluation call.

#### Scenario: Changed threshold applies on the next evaluation
- **WHEN** the same location consensus is evaluated twice, the second time with a lower
  frost warning threshold
- **THEN** the second frost alert reflects the new threshold

