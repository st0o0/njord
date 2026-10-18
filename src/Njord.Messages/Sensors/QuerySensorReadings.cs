using Njord.Sensors;
using Njord.Model;

namespace Njord.Messages.Sensors;

public sealed record QuerySensorReadings(LocationId Location);

public abstract record SensorReadingsResponse;
public sealed record SensorReadingsResult(SensorReadings Sensors) : SensorReadingsResponse;
public sealed record SensorReadingsNotAvailable(LocationId Location) : SensorReadingsResponse;
