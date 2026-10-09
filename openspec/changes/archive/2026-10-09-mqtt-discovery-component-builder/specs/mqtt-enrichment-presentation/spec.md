## ADDED Requirements

### Requirement: Discovery components are built through a shared spec/builder
Presenter implementations (`AlertPresenter`, `DerivedPresenter`, `IndexPresenter`,
`TrendPresenter`, `HistoryPresenter`, and the model/consensus discovery builders) SHALL
build each Home Assistant discovery component from a shared component-spec type mapped
through one shared builder function, rather than constructing a `JsonObject` literal
inline per component. `DiscoveryPayloadBuilder` SHALL NOT expose a `public static` method
with no caller in production code.

#### Scenario: Presenter builds components from specs
- **WHEN** `IndexPresenter.BuildDiscoveryPayload` is called
- **THEN** it produces a list of component specs (one per sensor) and maps them through the
  shared builder, with no `new JsonObject { ... }` literal written directly in the
  presenter method

#### Scenario: No unused public builder methods remain
- **WHEN** the architecture/dead-code check for `Njord.Mqtt` runs
- **THEN** every `public static` method on `DiscoveryPayloadBuilder` has at least one caller
  outside its own test file

#### Scenario: Removing a dead method path does not change output
- **WHEN** a presenter's component-building code is migrated from inline `JsonObject`
  literals to the shared spec/builder
- **THEN** its discovery payload Verify snapshot (per the existing "Presented payloads are
  byte-identical to the pre-split output" requirement) still matches the approved baseline
