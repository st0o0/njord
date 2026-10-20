using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record IceDetails(double ExpectedLow, int RainHours, bool SoilFrozen) : AlertDetails;
