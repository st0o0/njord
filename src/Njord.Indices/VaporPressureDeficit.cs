using Njord.Messages.Derived;
using Njord.Messages.Indices;

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
            < 0.4 => VpdCategory.Low,
            < 1.2 => VpdCategory.Optimal,
            < 2.0 => VpdCategory.High,
            _ => VpdCategory.Critical,
        };
        return new VpdInfo(category, vpd);
    }
}
