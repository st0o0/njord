## MODIFIED Requirements

### Requirement: Njord.Messages package dependencies
Njord.Messages SHALL reference the Akka.Streams NuGet package to support ISinkRef<T> and ISourceRef<T> in stream handshake message types. It SHALL NOT reference Akka.Actor, Akka.Cluster, or any actor-system infrastructure packages.

#### Scenario: Messages can define stream-ref message types
- **WHEN** a message record carries an ISinkRef<DomainDataReceived> or ISourceRef<PipelineEvent> field
- **THEN** it compiles in Njord.Messages without errors

#### Scenario: Messages does not depend on actor infrastructure
- **WHEN** Njord.Messages project references are inspected
- **THEN** it has no reference to Akka.Actor, Akka.Remote, Akka.Cluster, Akka.Persistence, or Akka.Hosting
