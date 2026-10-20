namespace Njord.Messages.Alerts;

public sealed record PressureDropDetails(double MaxDrop, int ModelsAgreeing) : AlertDetails;
