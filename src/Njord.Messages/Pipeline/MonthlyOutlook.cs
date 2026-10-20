using Njord.Messages.Alerts;

namespace Njord.Messages.Pipeline;

// Month is "yyyy-MM"; TempAnomaly in degrees Celsius, PrecipAnomaly in percent of the normal monthly precipitation.
public sealed record MonthlyOutlook(
    string Month,
    double TempAnomaly,
    double PrecipAnomaly,
    ConfidenceLevel Confidence);
