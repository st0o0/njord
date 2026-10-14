## ADDED Requirements

### Requirement: SensorHub pushes snapshot maps to the pipeline
The SensorHub SHALL push `SensorSnapshots` — an immutable map from `LocationId` to the aggregated `SensorSnapshot` of every location that has at least one fresh reading, with the staleness window applied at push time — to the pipeline resolved through the `IForecastPipeline` marker: after every accepted `UpdateReading`, on every expiry tick (every 60 seconds, also when nothing expired), and once when the pipeline is first resolved after the SensorHub started. While no pipeline is registered the SensorHub SHALL skip the push without failing; a rejected reading SHALL NOT trigger a push.

#### Scenario: Accepted reading is pushed
- **WHEN** the pipeline is registered and a reading `(Luzern, IndoorTemperature, "wohnzimmer", 23.5)` is accepted
- **THEN** the pipeline receives `SensorSnapshots` whose entry for "Luzern" holds `IndoorTemperature = 23.5`

#### Scenario: Map covers every location with fresh readings
- **WHEN** fresh readings exist for "Luzern" and "Bern" and another reading is accepted
- **THEN** the pushed map has entries for both "Luzern" and "Bern"

#### Scenario: Stale reading is left out of the map
- **WHEN** a reading older than the staleness window is stored for "Luzern" and no fresh reading exists for it
- **THEN** the pushed map has no entry for "Luzern"

#### Scenario: Rejected reading pushes nothing
- **WHEN** a reading outside its plausibility range is rejected
- **THEN** no `SensorSnapshots` is pushed for it

#### Scenario: No pipeline registered
- **WHEN** a reading is accepted while no pipeline is registered
- **THEN** the SensorHub replies `PushResult(true, null)` and keeps running
