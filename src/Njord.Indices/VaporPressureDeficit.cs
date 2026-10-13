using Njord.Compute.Contracts;

namespace Njord.Indices;

public static class VaporPressureDeficit
{
    public static VpdInfo? Categorize(double? temp, double? humidity)
    {
        if (temp is not { } t || humidity is not { } rh)
        {
            return null;
        }

        var svp = 0.6108 * Math.Exp(17.27 * t / (t + 237.3));
        var vpd = Math.Round(svp * (1 - rh / 100), 2);

        var category = vpd switch
        {
            < 0.4 => "low",
            < 1.2 => "optimal",
            < 2.0 => "high",
            _ => "critical",
        };
        return new VpdInfo(category, vpd);
    }
}
