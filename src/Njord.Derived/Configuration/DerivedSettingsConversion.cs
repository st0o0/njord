using Njord.Messages.Derived;

namespace Njord.Derived.Configuration;

public static class DerivedSettingsConversion
{
    public static DerivedSettings ToSettings(this DerivedOptions options, IEnumerable<int> horizons) =>
        new([.. horizons]);
}
