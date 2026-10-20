namespace Njord.Messages.Alerts;

public sealed record HumidityDetails(double MaxDewpoint, string? ComfortLevel) : AlertDetails;
