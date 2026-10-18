namespace Njord.Messages.Config;

public sealed record UpdateLocations(IReadOnlyList<LocationInput> Locations);

// Name is raw client input; it is validated by the configuration manager, so it is not a LocationId yet.
public sealed record LocationInput(string Name, double Latitude, double Longitude, IReadOnlyList<string> Models);

public abstract record UpdateLocationsResponse;
public sealed record UpdateLocationsCompleted(EffectiveConfig Config, IReadOnlyList<string> RestartRequired)
    : UpdateLocationsResponse;
public sealed record UpdateLocationsRejected(string Reason) : UpdateLocationsResponse;
public sealed record UpdateLocationsFailed(Exception Cause) : UpdateLocationsResponse;
