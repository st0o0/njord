using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record SnowDetails(double ExpectedAccumulation, double? FreezingLevel, int ModelsAgreeing) : AlertDetails;
