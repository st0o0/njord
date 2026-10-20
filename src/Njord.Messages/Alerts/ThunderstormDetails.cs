using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record ThunderstormDetails(int ModelsAgreeing) : AlertDetails;
