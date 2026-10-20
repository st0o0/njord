using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record TropicalNightDetails(double NightMinimum, int HoursAbove20) : AlertDetails;
