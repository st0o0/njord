using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record WindChillDetails(double FeltTemperature, double WindFactor, ExposureRisk? ExposureRisk) : AlertDetails;
