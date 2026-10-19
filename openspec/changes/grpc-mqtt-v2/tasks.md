## 1. gRPC proto redesign

- [x] 1.1 Design new proto messages for multi-domain data (WeatherData, AirQualityData, etc.)
- [x] 1.2 Update WeatherService RPCs for domain-aware data
- [x] 1.3 Remove old consensus/derived proto messages
- [x] 1.4 Update OpsService for multi-domain poll status
- [x] 1.5 Regenerate gRPC stubs

## 2. gRPC mapping layer

- [x] 2.1 Update/rewrite mapping extensions for new event types
- [x] 2.2 Update GrpcConsumer for new PipelineEvent types
- [x] 2.3 Update latest-value state for domain-based events
- [x] 2.4 Tests for all mappings

## 3. MQTT topic scheme

- [x] 3.1 Update TopicScheme for domain-based topics
- [x] 3.2 Update TopicSlug for new domain segments
- [x] 3.3 Tests for new topic scheme

## 4. MQTT payloads

- [x] 4.1 Remove old consensus/derived payload types
- [x] 4.2 Add domain-specific payload records
- [x] 4.3 Add alert payload records
- [x] 4.4 Update MqttPayloadJsonContext serializer
- [x] 4.5 Tests for all new payloads

## 5. MQTT publisher

- [x] 5.1 Update PipelineEventMqttMappings for new event types
- [x] 5.2 Update MqttStatePublisher event processing
- [x] 5.3 Tests for end-to-end MQTT flow

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx`
- [x] 6.2 All gRPC and MQTT tests pass
