using Njord.Forecast;

namespace Njord.Messages.Alerts;

public sealed record HeavyRainDetails(int HourlyExceedModels, int DailyExceedModels) : AlertDetails;
