## MODIFIED Requirements

### Requirement: Consumers receive new PipelineEvent types
The BroadcastHub SHALL emit new event types: domain-received events (WeatherDataReceived, AirQualityDataReceived, FloodDataReceived, SolarDataReceived, MarineDataReceived) and enrichment events (AlertRaised). Old event types (ForecastReceived, ConsensusComputed, DerivedComputed, TrendComputed, IndexComputed) are removed.

#### Scenario: GrpcConsumer processes new events
- **WHEN** a WeatherDataReceived event is emitted
- **THEN** the GrpcConsumer SHALL update its latest-value state and forward to streaming clients

#### Scenario: MqttStatePublisher processes new events
- **WHEN** an AlertRaised event is emitted
- **THEN** the MqttStatePublisher SHALL publish to the appropriate alert topic
