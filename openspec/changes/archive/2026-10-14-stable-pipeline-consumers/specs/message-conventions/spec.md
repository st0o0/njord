## MODIFIED Requirements

### Requirement: Njord.Messages organizes messages by domain namespace
The `Njord.Messages` project SHALL hold every public actor command, query, result and event, with one file per command or query (including its response hierarchy), organized into domain namespaces: `Budget/` (usage recording and status), `Config/` (config snapshot contracts, config, catalog and analysis-settings queries, update commands), `Forecast/` (fetch, fetch replies and rejection reasons, forecast queries), `Mqtt/` (connection events, inbound messages, subscribe), `Pipeline/` (`PipelineEvent`, scheduling, poll state), and `Sensors/` (readings, snapshot query, pushed snapshot maps and the request to push one). There SHALL be no generic acknowledgement message: a requester that needs an answer gets a typed reply from the domain's response hierarchy. No production library other than `Njord.Messages` SHALL declare a public message record; actor-private records stay `private` inside the actor.

#### Scenario: Mqtt messages in separate files
- **WHEN** inspecting `Njord.Messages/Mqtt/`
- **THEN** `MqttConnected`, `SubscribeInbound`, and `MqttInboundMessage` are each in their own `.cs` file

#### Scenario: SensorHub messages in separate files
- **WHEN** inspecting `Njord.Messages/Sensors/`
- **THEN** `UpdateReading`, `QuerySensorSnapshot` (with its response hierarchy), `SensorSnapshots` and `RequestSensorSnapshots` are each in their own file

#### Scenario: No public messages nested in actors
- **WHEN** any actor class in a feature library is inspected
- **THEN** it declares no public nested record

#### Scenario: Analysis settings query
- **WHEN** inspecting `Njord.Messages/Config/`
- **THEN** `QueryAnalysisSettings` and its response hierarchy (`AnalysisSettingsResponse`, `AnalysisSettingsResult`) are in their own file

#### Scenario: No acknowledgement message
- **WHEN** inspecting `Njord.Messages`
- **THEN** it declares no `Ack` record and no `Common/` namespace
