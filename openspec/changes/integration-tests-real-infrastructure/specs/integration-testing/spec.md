## ADDED Requirements

### Requirement: The real host is started in integration tests
`Njord.IntegrationTests` SHALL start the production composition (`NjordHost`, the same call `Program.cs` makes) on a test server with test configuration: a SQLite journal in a unique temporary directory, MQTT disabled unless the test needs it, a fake Open-Meteo client, and fixed locations and models. It SHALL assert the health endpoints and the metrics endpoint of the running host.

#### Scenario: Host becomes ready
- **WHEN** the host starts with valid test configuration and one poll cycle has completed through the fake client
- **THEN** `/healthz` and `/alive` return success and the metrics endpoint exposes the `njord_` meters

#### Scenario: Invalid configuration
- **WHEN** the host starts with an invalid configuration (for example a missing location)
- **THEN** startup fails with the validation message and the test asserts that message

### Requirement: gRPC services are exercised over a channel
Integration tests SHALL call the weather, ops, admin and sensor services through a `GrpcChannel` bound to the test server, including a streaming call and the `Unavailable` and `DeadlineExceeded` mappings.

#### Scenario: Config update through the channel
- **WHEN** a client updates the poll interval through the admin service
- **THEN** the response reports the applied change and a following config read returns the new value

#### Scenario: Actor stopped
- **WHEN** the poll manager actor is stopped and the ops service is called
- **THEN** the call fails with status `Unavailable`

### Requirement: Persistence survives a host restart
Integration tests SHALL run two host lifetimes over the same SQLite file and assert that the poll worker state and the budget usage of the first lifetime are present in the second.

#### Scenario: Budget usage recovered
- **WHEN** the first host records API calls and is stopped, and a second host starts on the same database
- **THEN** the budget status of the second host reports the recorded usage

#### Scenario: Poll state recovered
- **WHEN** a poll worker has learned its cycle in the first lifetime
- **THEN** the second lifetime reports the worker in the steady phase with the same cycle

### Requirement: MQTT publishing is verified against a broker
Integration tests SHALL run a pinned Mosquitto container through Testcontainers and assert retained state payloads, the retained service status, the last will on abrupt disconnect, and republish after a broker restart, using a second MQTT client as the observer. They SHALL fail with a clear message when Docker is unavailable and SHALL NOT use dynamic skips.

#### Scenario: Retained payload matches the golden master
- **WHEN** the consensus state of the analysis fixture is published
- **THEN** a new subscriber receives a retained message whose payload equals the golden-master payload

#### Scenario: Last will
- **WHEN** the publisher connection is dropped without a clean disconnect
- **THEN** the retained service status becomes `offline`

### Requirement: Integration tests are isolated and bounded
Each integration fixture SHALL use unique temporary directories (database and override file), its own broker container, and explicit timeouts from `TestTimeouts`; host-based fixtures SHALL run one after another because the Akka remoting port is fixed, and SHALL dispose hosts and containers deterministically.

#### Scenario: Consecutive test classes
- **WHEN** two integration test classes run one after another
- **THEN** they use different databases and brokers and do not observe each other's data
