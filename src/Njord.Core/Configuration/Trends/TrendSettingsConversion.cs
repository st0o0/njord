using Njord.Compute.Settings;

namespace Njord.Core.Configuration.Trends;

public static class TrendSettingsConversion
{
    public static TrendSettings ToSettings(this TrendOptions options) => TrendSettings.Default;
}
