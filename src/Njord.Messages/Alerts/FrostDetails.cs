using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record FrostDetails(double ExpectedLow, Horizon? EarliestFrost, int ModelsAgreeing) : AlertDetails;
