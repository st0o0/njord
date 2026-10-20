using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record FogDetails(int FogHours, int ModelsAgreeing) : AlertDetails;
