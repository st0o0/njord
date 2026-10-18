using Njord.Messages.Trends;

namespace Njord.Trends.Configuration;

public static class TrendSettingsConversion
{
    public static TrendSettings ToSettings(this TrendOptions options) => TrendSettings.Default;
}
