namespace Njord.Messages.Alerts;

public sealed record HeatDetails(double ExpectedMax, int ModelsAgreeing) : AlertDetails;
