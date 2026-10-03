# mqtt-enrichment-presentation Specification

## Purpose

MQTT presentation of enrichment results: the presenter registry in `Njord.Mqtt` that turns computed enrichment results into Home Assistant discovery payloads and state messages.

## Requirements

### Requirement: Presenters own the MQTT presentation of enrichment results
`Njord.Mqtt` SHALL define `IEnrichmentPresenter` with members `TypeName` (string), `Enabled` (bool), `DeviceId(string location)`, `BuildDiscoveryPayload(DiscoveryContext ctx, string location)` and `ToStateMessages(object result, string baseTopic, string location)`, and SHALL provide exactly one presenter per enrichment feature `TypeName` (`alerts`, `derived`, `trends`, `indices`, `history`) plus exactly one presenter for the pipeline result `consensus`. `Njord.Mqtt` SHALL contain no special-case dispatch for any `TypeName` outside the presenter registry. `Njord.Enrichment` SHALL NOT reference `Njord.Mqtt` and SHALL NOT contain Home Assistant or MQTT knowledge (topics, discovery JSON, payload JSON).

#### Scenario: Presenter set matches the feature set plus consensus
- **WHEN** the registered `IEnrichmentFeature` type names and the registered `IEnrichmentPresenter` type names are compared
- **THEN** every feature `TypeName` has exactly one presenter with the same `TypeName`, and the only presenter without a feature is `consensus`

#### Scenario: Enrichment has no MQTT dependency
- **WHEN** the architecture tests run
- **THEN** the `Njord.Enrichment` assembly has no project reference to `Njord.Mqtt` and no type in it depends on a `Njord.Mqtt` type

#### Scenario: Consensus is presented through the registry
- **WHEN** an `EnrichmentUpdate` with `TypeName = "consensus"` and a `ConsensusResult` is dispatched, or discovery is published for a location
- **THEN** `MqttEgressActor` and `DiscoveryActor` use the `consensus` presenter from the registry with no consensus-specific branch, the produced topics and payloads equal those of the former hardwired path (`BuildConsensus` / `FromConsensus`), and `consensus` is still not an `IEnrichmentFeature`

#### Scenario: Unknown type name is ignored
- **WHEN** an `EnrichmentUpdate` arrives whose `TypeName` has no registered presenter
- **THEN** no MQTT messages are produced for it, as before the split

#### Scenario: Registry order is preserved
- **WHEN** discovery is published for a location
- **THEN** the per-model devices come first, then the presenter devices in the order consensus, alerts, derived, trends, indices, history, as before the split

### Requirement: Presenters decide enablement from the same configuration as features
For each enrichment feature, the presenter's `Enabled` SHALL equal the `Enabled` of the feature with the same `TypeName`, both derived from `EnrichmentOptions` through one shared lookup in `Njord.Core`. The `consensus` presenter SHALL keep today's behavior: its `Enabled` is constant `true`, because the consensus device configuration is published for every location regardless of `EnrichmentOptions.Consensus.Enabled`; `Consensus.Enabled` continues to gate only whether the enrichment actor emits consensus events.

#### Scenario: Disabled feature publishes no discovery
- **WHEN** `EnrichmentOptions.Alerts.Enabled` is `false`
- **THEN** the alerts feature is not computed and `DiscoveryActor` publishes no alerts device config for any location

#### Scenario: Enablement cannot diverge
- **WHEN** any `EnrichmentOptions.<Feature>.Enabled` value of alerts, derived, trends, indices or history is toggled
- **THEN** the feature and its presenter report the same `Enabled` value

#### Scenario: Consensus device is announced regardless of the consensus toggle
- **WHEN** `EnrichmentOptions.Consensus.Enabled` is `false`
- **THEN** `DiscoveryActor` still publishes the consensus device config for every location and no consensus state messages are produced because the enrichment actor emits no consensus events

### Requirement: Presented payloads are byte-identical to the pre-split output
For identical configuration, locations and results, each presenter SHALL produce exactly the discovery payload string and the state messages (topic, payload, retain) that the corresponding feature produced before the split. The entity set SHALL remain static and derived from configuration (locations × enabled features × parameters/horizons/day offsets), never from the results received.

#### Scenario: Discovery payload snapshots
- **WHEN** each presenter, including `consensus`, builds its discovery payload for a fixed configuration with all features enabled
- **THEN** the output equals the approved Verify snapshot recorded against the pre-split code

#### Scenario: State message snapshots
- **WHEN** each presenter, including `consensus`, maps a fixed result to state messages
- **THEN** the topics, payload strings and retain flags equal the approved Verify snapshot recorded against the pre-split code

#### Scenario: Missing value stays an unavailable state
- **WHEN** a result lacks a value for a configured entity
- **THEN** the entity still exists in the discovery payload and its state is rendered as before (unavailable), not omitted
