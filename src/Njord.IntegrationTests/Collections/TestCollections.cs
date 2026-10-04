using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests.Collections;

[CollectionDefinition("Health")]
public sealed class HealthCollection : ICollectionFixture<NjordFixture>;

[CollectionDefinition("Weather")]
public sealed class WeatherCollection : ICollectionFixture<NjordFixture>;

[CollectionDefinition("Ops")]
public sealed class OpsCollection : ICollectionFixture<NjordFixture>;

[CollectionDefinition("Admin")]
public sealed class AdminCollection : ICollectionFixture<NjordFixture>;

[CollectionDefinition("Sensor")]
public sealed class SensorCollection : ICollectionFixture<NjordFixture>;
