using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record VisibilityDetails(double MinVisibility, int HoursBelow1000m) : AlertDetails;
