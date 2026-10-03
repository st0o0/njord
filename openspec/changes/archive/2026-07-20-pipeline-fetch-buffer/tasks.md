## 1. Pipeline buffer and hub reduction

- [x] 1.1 In `src/Njord/Pipeline/PipelineActor.cs`, add `.Buffer(32, OverflowStrategy.Backpressure)` between the `SelectAsyncUnordered(2)` output and `.To(broadcastHubSink)`.
- [x] 1.2 In `src/Njord/Pipeline/PipelineActor.cs`, reduce `BroadcastHub.Sink<FetchOutcome>(bufferSize: 16)` to `bufferSize: 2`.

## 2. Enrichment hub restructure

- [x] 2.1 In `src/Njord/Enrichment/EnrichmentActor.cs`, add `.Buffer(8, OverflowStrategy.Backpressure)` before the `snapshotHubSink` (after `.Where(snap => snap.HasChanged)`).
- [x] 2.2 In `src/Njord/Enrichment/EnrichmentActor.cs`, reduce `BroadcastHub.Sink<ModelSnapshot>(bufferSize: 8)` to `bufferSize: 1`.

## 3. Egress hub reduction

- [x] 3.1 In `src/Njord/Egress/EgressActor.cs`, reduce `BroadcastHub.Sink<EgressEvent>(bufferSize: 16)` to `bufferSize: 4`.

## 4. MQTT dedup by hash

- [x] 4.1 In `src/Njord/Mqtt/MqttEgressActor.cs`, change `lastPublished` from `Dictionary<string, string>` to `Dictionary<string, int>`. Store `msg.Payload.GetHashCode()` instead of the full payload. Compare against the stored hash.

## 5. Journal trimming

- [x] 5.1 In `src/Njord/Enrichment/ForecastHistoryActor.cs`, on `SaveSnapshotSuccess`, call `DeleteMessages(success.Metadata.SequenceNr)` and `DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1))` to trim old journal entries and snapshots.
- [x] 5.2 Handle `DeleteMessagesSuccess` and `DeleteSnapshotSuccess` (no-op handlers to prevent dead letters).

## 6. Validation

- [x] 6.1 Run `dotnet build Njord.slnx` from `src/` and confirm clean build.
- [x] 6.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` and confirm all tests pass. (562 total, 6 pre-existing failures in Persistence serialization specs — CRLF in .verified.txt files, unrelated to this change)
