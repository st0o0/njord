namespace Njord.Messages.Alerts;

public sealed record HumidityDetails(double MaxDewpoint, HumidityComfort? ComfortLevel) : AlertDetails;
