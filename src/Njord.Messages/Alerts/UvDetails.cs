namespace Njord.Messages.Alerts;

public sealed record UvDetails(UvLevel Level, double UvIndex) : AlertDetails;
