namespace Njord.Messages.Alerts;

public sealed record WindChillDetails(double FeltTemperature, double WindFactor, string? ExposureRisk) : AlertDetails;
