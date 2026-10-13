using Njord.Compute.Settings;

namespace Njord.Core.Configuration.Derived;

public static class DerivedSettingsConversion
{
    public static DerivedSettings ToSettings(this DerivedOptions options, IEnumerable<int> horizons) =>
        new([.. horizons]);
}
