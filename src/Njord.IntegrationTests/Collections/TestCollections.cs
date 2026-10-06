using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests.Collections;

[CollectionDefinition("Health")]
public sealed class HealthCollection : ICollectionFixture<NjordFixture>;

[CollectionDefinition("Ops")]
public sealed class OpsCollection : ICollectionFixture<NjordFixture>;

[CollectionDefinition("Sensor")]
public sealed class SensorCollection : ICollectionFixture<NjordFixture>;
