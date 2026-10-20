namespace Njord.Messages.Alerts;

public sealed record StormDetails(double ExpectedMaxGust, int ModelsAgreeing) : AlertDetails;
